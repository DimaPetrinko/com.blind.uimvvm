#nullable enable
namespace Blind.UiMvvm.Navigation
{
	/// <summary>
	/// What opening a screen does to the screens beneath it. Each opener names its screen's default in
	/// <see cref="IScreenOpener{TScreen}.DefaultOpening"/>, and a single
	/// <see cref="IScreenService{TScreen}.Open(TScreen, ScreenOpening?)"/> call can override it.
	/// </summary>
	public enum ScreenOpening
	{
		/// <summary>Hides every screen visible beneath it until it closes.</summary>
		Cover,

		/// <summary>Leaves the screens beneath it as they are: a popup over a screen that stays in view.</summary>
		Overlay,
	}
}
