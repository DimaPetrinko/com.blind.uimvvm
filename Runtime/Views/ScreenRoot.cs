#nullable enable
namespace Blind.UiMvvm.Views
{
	/// <summary>
	/// A named scope in the document that child views resolve inside, with no view model of its own.
	///
	/// <see cref="View{TViewModel}"/> covers a screen that has something to bind; this covers one that
	/// is only a container. Put it on the GameObject that mirrors the screen's root element and the
	/// views underneath resolve their own roots inside it, which is what lets a name be unique per
	/// screen rather than per document.
	/// </summary>
	public sealed class ScreenRoot : ViewRoot
	{
	}
}
