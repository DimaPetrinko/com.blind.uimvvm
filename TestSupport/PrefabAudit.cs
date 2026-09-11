#nullable enable
using System.Collections.Generic;
using System.Linq;
using Blind.UiMvvm.Views;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.TestSupport
{
	/// <summary>
	/// Checks a screen prefab against the UXML its <see cref="UIDocument"/> points at.
	///
	/// Every <see cref="ViewRoot"/> carries a serialized <c>Root Name</c> that must name an element in
	/// that document, resolved inside the nearest ancestor view - a link nothing verifies until the
	/// screen is opened. This walks the prefab the way the runtime walks the scene.
	/// </summary>
	public static class PrefabAudit
	{
		/// <summary>Every ViewRoot in the prefab resolves its Root Name, in its own scope.</summary>
		public static AuditResult RootNamesResolve(GameObject screenPrefab)
		{
			if (screenPrefab == null)
				return AuditResult.Failed(new[] { "no prefab was given" });

			var views = screenPrefab.GetComponentsInChildren<ViewRoot>(true);
			if (views.Length == 0)
				return AuditResult.Failed(new[] { $"{screenPrefab.name} contains no ViewRoot" });

			var trees = new Dictionary<VisualTreeAsset, VisualElement>();
			var problems = new List<string>();

			foreach (var view in views)
			{
				var document = view.GetComponentInParent<UIDocument>(true);
				if (document == null)
					document = view.GetComponent<UIDocument>();

				if (document == null || document.visualTreeAsset == null)
				{
					problems.Add($"{Describe(view)} has no UIDocument with a UXML asset above it");
					continue;
				}

				if (!trees.TryGetValue(document.visualTreeAsset, out var tree))
				{
					tree = document.visualTreeAsset.Instantiate();
					trees[document.visualTreeAsset] = tree;
				}

				var problem = ResolveIn(tree, view, document.visualTreeAsset.name);
				if (problem != null)
					problems.Add(problem);
			}

			return problems.Count == 0 ? AuditResult.Passed : AuditResult.Failed(problems);
		}

		private static string? ResolveIn(VisualElement documentRoot, ViewRoot view, string documentName)
		{
			var scope = documentRoot;

			foreach (var ancestor in AncestorsOf(view))
			{
				var narrowed = scope.Q(ancestor.RootName);
				if (narrowed == null)
					return $"{Describe(view)} cannot be resolved: its ancestor {Describe(ancestor)} " +
						   $"names '{ancestor.RootName}', which is not in {documentName}";

				scope = narrowed;
			}

			return scope.Q(view.RootName) != null
				? null
				: $"{Describe(view)} names '{view.RootName}', which does not exist in its scope in {documentName}";
		}

		private static IReadOnlyList<ViewRoot> AncestorsOf(ViewRoot view)
		{
			var ancestors = new List<ViewRoot>();

			for (var parent = view.transform.parent; parent != null; parent = parent.parent)
			{
				var ancestor = parent.GetComponent<ViewRoot>();
				if (ancestor != null)
					ancestors.Add(ancestor);
			}

			ancestors.Reverse();
			return ancestors;
		}

		private static string Describe(ViewRoot view) => $"{view.GetType().Name} on '{PathOf(view.transform)}'";

		private static string PathOf(Transform transform)
		{
			var names = new List<string>();

			for (var current = transform; current != null; current = current.parent)
				names.Add(current.name);

			names.Reverse();
			return string.Join("/", names);
		}
	}
}
