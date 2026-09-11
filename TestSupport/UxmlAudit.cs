#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Xml.Linq;
using Unity.Properties;
using UnityEditor;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.TestSupport
{
	/// <summary>
	/// Checks the links between a UXML file and the C# that expects it - the element names a view looks
	/// up, and the <c>data-source-path</c> values a binding resolves against a view model. Neither is
	/// visible to the compiler, so renaming either half breaks silently at run time.
	///
	/// The checks are generic; only the pairing of a document with its view model is a project fact, so
	/// that stays in the project's own tests.
	/// </summary>
	public static class UxmlAudit
	{
		private const BindingFlags MemberLookup =
			BindingFlags.Public | BindingFlags.Instance | BindingFlags.FlattenHierarchy;

		/// <summary>Every distinct <c>data-source-path</c> declared in the document, in document order.</summary>
		public static IReadOnlyList<string> BindingPaths(string uxmlPath)
		{
			if (!File.Exists(uxmlPath))
				return new string[0];

			return XDocument.Load(uxmlPath)
				.Descendants()
				.Select(element => element.Attribute("data-source-path"))
				.Where(attribute => attribute != null && !string.IsNullOrWhiteSpace(attribute.Value))
				.Select(attribute => attribute!.Value)
				.Distinct()
				.ToList();
		}

		/// <summary>
		/// Every <c>data-source-path</c> in the document resolves to a bindable member of the view model.
		///
		/// Nested paths (<c>Player.Stats.Health</c>) are followed segment by segment, and an indexed
		/// segment (<c>Items[0]</c>) continues through the collection's element type. Interfaces are
		/// walked as well as classes, because a view model is usually bound through one.
		/// </summary>
		public static AuditResult BindingPathsResolve(string uxmlPath, Type viewModelType)
		{
			if (viewModelType == null)
				throw new ArgumentNullException(nameof(viewModelType));

			if (!File.Exists(uxmlPath))
				return AuditResult.Failed(new[] { $"could not read {uxmlPath}" });

			var paths = BindingPaths(uxmlPath);
			if (paths.Count == 0)
				return AuditResult.Failed(new[] { $"{uxmlPath} declares no bindings" });

			var problems = paths
				.Select(path => Resolve(viewModelType, path, uxmlPath))
				.Where(problem => problem != null)
				.Select(problem => problem!)
				.ToList();

			return problems.Count == 0 ? AuditResult.Passed : AuditResult.Failed(problems);
		}

		/// <summary>Every name a view looks up exists in the document.</summary>
		public static AuditResult ElementsExist(string uxmlPath, params string[] names)
		{
			var root = Instantiate(uxmlPath);
			if (root == null)
				return AuditResult.Failed(new[] { $"could not load {uxmlPath}" });

			var problems = names
				.Where(name => root.Q(name) == null)
				.Select(name => $"{uxmlPath} has no element named '{name}'")
				.ToList();

			return problems.Count == 0 ? AuditResult.Passed : AuditResult.Failed(problems);
		}

		/// <summary>
		/// Each name appears exactly once. Scoped root resolution tells two instances of one template
		/// apart by name, so a duplicate makes which one a view binds to an accident of document order.
		/// </summary>
		public static AuditResult NamesAreUnique(string uxmlPath, params string[] names)
		{
			var root = Instantiate(uxmlPath);
			if (root == null)
				return AuditResult.Failed(new[] { $"could not load {uxmlPath}" });

			var problems = new List<string>();

			foreach (var name in names)
			{
				var matches = root.Query(name).ToList().Count;
				if (matches != 1)
					problems.Add($"{uxmlPath} contains {matches} elements named '{name}'; expected exactly one");
			}

			return problems.Count == 0 ? AuditResult.Passed : AuditResult.Failed(problems);
		}

		private static VisualElement? Instantiate(string uxmlPath)
		{
			var asset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(uxmlPath);
			return asset == null ? null : asset.Instantiate();
		}

		private static string? Resolve(Type viewModelType, string path, string uxmlPath)
		{
			var owner = viewModelType;

			foreach (var rawSegment in path.Split('.'))
			{
				var indexed = rawSegment.EndsWith("]", StringComparison.Ordinal);
				var bracket = rawSegment.IndexOf('[');
				var segment = bracket < 0 ? rawSegment : rawSegment.Substring(0, bracket);

				if (segment.Length == 0)
					return $"{uxmlPath} binds '{path}', which is not a usable path";

				var member = FindMember(owner, segment);
				if (member == null)
					return DescribeMissing(owner, segment, path, uxmlPath);

				owner = member;

				if (!indexed)
					continue;

				var element = ElementTypeOf(owner);
				if (element == null)
					return $"{uxmlPath} binds '{path}', but '{segment}' is a {owner.Name}, which is not indexable";

				owner = element;
			}

			return null;
		}

		private static string DescribeMissing(Type owner, string segment, string path, string uxmlPath)
		{
			// An existing member without the attribute is the far more common mistake, and saying so
			// saves the reader from hunting for a name that is right there.
			var unmarked = TypeAndInterfaces(owner).Any(type =>
				type.GetProperty(segment, MemberLookup) != null || type.GetField(segment, MemberLookup) != null);

			return unmarked
				? $"{uxmlPath} binds '{path}', and {owner.Name}.{segment} exists but carries no [CreateProperty]"
				: $"{uxmlPath} binds '{path}', which is not a [CreateProperty] member of {owner.Name}";
		}

		private static Type? FindMember(Type owner, string name)
		{
			foreach (var type in TypeAndInterfaces(owner))
			{
				var property = type.GetProperty(name, MemberLookup);
				if (property != null && IsBindable(property))
					return property.PropertyType;

				var field = type.GetField(name, MemberLookup);
				if (field != null && IsBindable(field))
					return field.FieldType;
			}

			return null;
		}

		private static bool IsBindable(MemberInfo member) => member.IsDefined(typeof(CreatePropertyAttribute), true);

		/// <summary>
		/// A view model is normally bound through its interface, and <see cref="Type.GetProperty(string,BindingFlags)"/>
		/// on an interface does not see the interfaces it extends.
		/// </summary>
		private static IEnumerable<Type> TypeAndInterfaces(Type type)
		{
			yield return type;

			if (!type.IsInterface)
				yield break;

			foreach (var inherited in type.GetInterfaces())
				yield return inherited;
		}

		private static Type? ElementTypeOf(Type type)
		{
			if (type.IsArray)
				return type.GetElementType();

			var enumerable = TypeAndInterfaces(type)
				.Concat(type.GetInterfaces())
				.FirstOrDefault(candidate =>
					candidate.IsGenericType && typeof(IEnumerable).IsAssignableFrom(candidate));

			return enumerable?.GetGenericArguments().FirstOrDefault();
		}
	}
}
