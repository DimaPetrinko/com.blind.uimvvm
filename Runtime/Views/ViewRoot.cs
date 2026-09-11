#nullable enable
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Views
{
	/// <summary>
	/// Non-generic base for views. Owns the UXML root name and resolves that root <b>relative to the
	/// nearest ancestor <see cref="ViewRoot"/></b>, falling back to the <see cref="UIDocument"/> root
	/// when there is none. Scoping the lookup is what lets several instances of the same template each
	/// be bound: a name only has to be unique inside its parent's subtree, not across the document.
	/// The parent lookup needs a common non-generic type, which is why this class exists separately
	/// from <see cref="View{TViewModel}"/>.
	/// </summary>
	public abstract class ViewRoot : MonoBehaviour
	{
		[SerializeField] private string m_RootName = "root";

		private VisualElement? mResolvedRoot;

		public string RootName => m_RootName;

		/// <summary>
		/// Resolves and caches this view's root element. Recurses up the parent chain by name rather
		/// than reading a parent's bound root, so bind order does not matter.
		///
		/// A cached element whose <see cref="VisualElement.panel"/> has gone is a detached leftover: a
		/// <see cref="UIDocument"/> rebuilds its visual tree whenever it is re-enabled, and every
		/// element from the old tree is orphaned. Resolving again rather than handing that back is what
		/// lets a screen be shown by re-enabling it and re-bound, instead of only by being destroyed
		/// and recreated.
		/// </summary>
		public VisualElement ResolveRoot()
		{
			if (mResolvedRoot != null && mResolvedRoot.panel != null)
				return mResolvedRoot;

			var root = ResolveScope().Q(m_RootName);
			if (root == null)
				throw new MissingComponentException(
					$"{GetType().Name}: no VisualElement named '{m_RootName}' under its scope");

			mResolvedRoot = root;
			return root;
		}

		/// <summary>Drops the cached element so the next <see cref="ResolveRoot"/> looks it up again.</summary>
		public void InvalidateRoot() => mResolvedRoot = null;

		private VisualElement ResolveScope()
		{
			var parentView = transform.parent == null
				? null
				: transform.parent.GetComponentInParent<ViewRoot>(true);

			if (parentView != null)
				return parentView.ResolveRoot();

			var document = GetComponentInParent<UIDocument>();
			if (document == null)
				document = GetComponent<UIDocument>();
			if (document == null)
				throw new MissingComponentException(
					$"{GetType().Name} requires a UIDocument on itself or on a parent");

			return document.rootVisualElement;
		}
	}
}
