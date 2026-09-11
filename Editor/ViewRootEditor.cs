#nullable enable
using System.Collections.Generic;
using System.Linq;
using Blind.UiMvvm.Views;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Editor
{
	/// <summary>
	/// Turns <c>Root Name</c> from a free-text string into a list of the names that actually exist in
	/// the UXML, resolved through the same scope the runtime uses.
	///
	/// That link is the one thing about this package the compiler cannot see: a renamed element leaves
	/// the field pointing at nothing and the screen throws the first time it binds. Picking from the
	/// document removes the typo case entirely, and a name that no longer resolves is called out here
	/// rather than at play time.
	/// </summary>
	[CustomEditor(typeof(ViewRoot), true)]
	[CanEditMultipleObjects]
	public class ViewRootEditor : UnityEditor.Editor
	{
		private const string RootNameProperty = "m_RootName";

		public override void OnInspectorGUI()
		{
			serializedObject.Update();

			var rootName = serializedObject.FindProperty(RootNameProperty);

			if (rootName == null || targets.Length > 1)
			{
				DrawDefaultInspector();
				return;
			}

			DrawPropertiesExcluding(serializedObject, RootNameProperty, "m_Script");
			DrawRootName(rootName, (ViewRoot)target);

			serializedObject.ApplyModifiedProperties();
		}

		private static void DrawRootName(SerializedProperty rootName, ViewRoot view)
		{
			var names = NamesInScope(view);

			if (names == null)
			{
				EditorGUILayout.PropertyField(rootName, new GUIContent("Root Name"));
				EditorGUILayout.HelpBox(
					"No UIDocument with a UXML asset was found on this object or its parents, so the available element names cannot be listed.",
					MessageType.Info);
				return;
			}

			if (names.Count == 0)
			{
				EditorGUILayout.PropertyField(rootName, new GUIContent("Root Name"));
				EditorGUILayout.HelpBox("The UXML in scope declares no named elements.", MessageType.Warning);
				return;
			}

			var current = rootName.stringValue;
			var isMissing = !names.Contains(current);

			// A name that no longer resolves stays selectable, so the field still shows what is actually
			// stored and the fix is one click rather than a retype.
			var options = isMissing
				? new[] { $"{current}  (missing)" }.Concat(names).ToArray()
				: names.ToArray();

			var index = isMissing ? 0 : names.IndexOf(current);
			var chosen = EditorGUILayout.Popup("Root Name", index, options);

			if (chosen != index)
				rootName.stringValue = isMissing ? options[chosen] : names[chosen];

			if (isMissing)
				EditorGUILayout.HelpBox(
					$"No element named '{current}' exists in this view's scope. Binding will throw a MissingComponentException.",
					MessageType.Error);
		}

		/// <summary>
		/// The names visible to this view, resolved the way <see cref="ViewRoot.ResolveRoot"/> resolves
		/// at runtime: inside the nearest ancestor view's root, or the whole document when there is
		/// none. Null when there is no document to read.
		/// </summary>
		private static List<string>? NamesInScope(ViewRoot view)
		{
			var document = view.GetComponentInParent<UIDocument>(true);
			if (document == null)
				document = view.GetComponent<UIDocument>();
			if (document == null || document.visualTreeAsset == null)
				return null;

			VisualElement scope = document.visualTreeAsset.Instantiate();

			// Outermost first, so each step narrows into the previous one exactly as the runtime does.
			foreach (var ancestor in AncestorsOf(view))
			{
				var narrowed = scope.Q(ancestor.RootName);
				if (narrowed == null)
					return new List<string>();

				scope = narrowed;
			}

			return scope.Query<VisualElement>()
				.ToList()
				.Select(element => element.name)
				.Where(name => !string.IsNullOrEmpty(name))
				.Distinct()
				.OrderBy(name => name)
				.ToList();
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
	}
}
