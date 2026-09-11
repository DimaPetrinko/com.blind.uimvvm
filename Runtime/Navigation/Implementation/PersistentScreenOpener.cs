#nullable enable
using System;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Navigation.Implementation
{
	/// <summary>
	/// A screen created once and then only shown and hidden - a hub, a HUD, anything at the bottom of a
	/// stack. Closing covers it rather than destroying it.
	///
	/// Hidden rather than disabled on purpose: disabling a <see cref="UIDocument"/> makes it rebuild its
	/// visual tree when it comes back, which would strand every already-bound view on the old tree.
	/// </summary>
	public abstract class PersistentScreenOpener<TScreen, TInstance> : ScreenOpener<TScreen, TInstance>
		where TScreen : struct, Enum
		where TInstance : class
	{
		private readonly ScreenCover mCover;

		protected PersistentScreenOpener(
			TScreen screen,
			ScreenCover cover = default,
			Action<string>? logWarning = null,
			Action<string>? logError = null)
			: base(screen, logWarning, logError)
		{
			mCover = cover;
		}

		protected sealed override bool ReusesInstance => true;

		/// <summary>Mandatory here: a screen that hides by styling its root cannot do it without one.</summary>
		protected abstract override VisualElement? ResolveRoot(TInstance instance);

		public override void SetCovered(bool covered)
		{
			var root = Root;
			if (root == null)
				return;

			mCover.Apply(root, covered);
		}

		protected override void OnPushed() => SetCovered(false);

		protected override void OnPopped() => SetCovered(true);
	}
}
