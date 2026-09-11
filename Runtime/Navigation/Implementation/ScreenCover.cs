#nullable enable
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Navigation.Implementation
{
	/// <summary>
	/// What "covered" looks like for a screen that stays alive underneath another one.
	///
	/// A value rather than a subclass, because the interesting part is <i>which</i> class - a type name
	/// cannot carry that, and a struct is testable against a bare <see cref="VisualElement"/> with no
	/// MonoBehaviour or panel in sight.
	/// </summary>
	public readonly struct ScreenCover
	{
		private readonly string? mCoveredClass;

		private ScreenCover(string? coveredClass)
		{
			mCoveredClass = coveredClass;
		}

		/// <summary>
		/// Covered collapses the screen outright. Nothing shows through, which is what two screens with
		/// different chrome need. Also what <c>default(ScreenCover)</c> does.
		/// </summary>
		public static ScreenCover Display => new(null);

		/// <summary>
		/// Covered toggles a class, so the stylesheet decides what covered looks like - a fade, a blur -
		/// and can transition into it. For a screen that sits over something still worth seeing.
		/// </summary>
		public static ScreenCover UssClass(string coveredClass) => new(coveredClass);

		public void Apply(VisualElement root, bool covered)
		{
			if (root == null)
				return;

			if (mCoveredClass == null)
				root.style.display = covered ? DisplayStyle.None : DisplayStyle.Flex;
			else
				root.EnableInClassList(mCoveredClass, covered);
		}
	}
}
