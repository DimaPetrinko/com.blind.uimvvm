#nullable enable
namespace Blind.UiMvvm.Binding
{
	/// <summary>
	/// Binds one screen's views to their view models, once.
	///
	/// A screen's opener resolves this from the screen's own scope and calls it immediately after
	/// creating the screen, while the container and the freshly built visual tree both already exist.
	/// That is the point: the first frame the screen renders is already bound.
	///
	/// Deliberately *not* an entry point contract. Running this from a DI container's start phase lands
	/// a frame after the open that created it, because a screen is usually opened from an input event
	/// dispatched late in the frame while start phases run early in the next one - and the screen then
	/// renders one frame of whatever placeholder values its UXML was authored with.
	/// </summary>
	public interface IViewBinder
	{
		void Bind();
	}
}
