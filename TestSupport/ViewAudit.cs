#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Blind.UiMvvm.Views;

namespace Blind.UiMvvm.TestSupport
{
	/// <summary>
	/// Checks view classes for the one mistake this package cannot prevent at compile time.
	/// </summary>
	public static class ViewAudit
	{
		/// <summary>
		/// No view declares its own <c>OnDestroy</c>.
		///
		/// <see cref="View{TViewModel}"/> unwires everything from <c>OnDestroy</c>, and Unity dispatches
		/// the most derived one only. A subclass that declares its own - private, as the pattern used to
		/// be written - hides the base's rather than overriding it, and nothing is ever unwired. It
		/// compiles, it runs, and every subscription outlives the screen. Override <c>OnUnbind</c>
		/// instead.
		/// </summary>
		public static AuditResult NoViewDeclaresOnDestroy(params Assembly[] assemblies)
		{
			if (assemblies == null || assemblies.Length == 0)
				throw new ArgumentException("At least one assembly is required", nameof(assemblies));

			var problems = assemblies
				.SelectMany(TypesOf)
				.Where(IsView)
				.Select(DeclaredOnDestroyProblem)
				.Where(problem => problem != null)
				.Select(problem => problem!)
				.ToList();

			return problems.Count == 0 ? AuditResult.Passed : AuditResult.Failed(problems);
		}

		private static string? DeclaredOnDestroyProblem(Type type)
		{
			var declared = type.GetMethod(
				"OnDestroy",
				BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);

			if (declared == null)
				return null;

			// An override reports the base's declaration; a method that merely hides one reports itself.
			if (declared.GetBaseDefinition().DeclaringType != declared.DeclaringType)
				return null;

			return $"{type.FullName} declares its own OnDestroy, which hides View<>.OnDestroy so nothing " +
				   "it wired is ever unwired. Override OnUnbind instead.";
		}

		private static bool IsView(Type type)
		{
			if (type.IsAbstract || !typeof(Views.ViewRoot).IsAssignableFrom(type))
				return false;

			for (var current = type.BaseType; current != null; current = current.BaseType)
				if (current.IsGenericType && current.GetGenericTypeDefinition() == typeof(View<>))
					return true;

			return false;
		}

		private static IEnumerable<Type> TypesOf(Assembly assembly)
		{
			try
			{
				return assembly.GetTypes();
			}
			catch (ReflectionTypeLoadException e)
			{
				return e.Types.Where(type => type != null).Select(type => type!);
			}
		}
	}
}
