#nullable enable
using System;
using Cysharp.Threading.Tasks;

namespace Blind.UiMvvm.Navigation
{
	/// <summary>
	/// A stack of screens. <see cref="Open"/> pushes, <see cref="Close"/> pops, so a back button and a
	/// screen's own close button travel the same path and nested screens need no extra plumbing.
	///
	/// Both return a <see cref="UniTask{T}"/> because an opener may need to load before it is ready.
	/// The result says whether the transition happened: a request that arrives while another is still
	/// running is refused, an opener may decline to open, and neither is distinguishable from success
	/// otherwise. A caller with nothing to sequence afterwards can drop the task with <c>Forget()</c>;
	/// the transition still runs, and still runs synchronously when every opener involved is
	/// synchronous.
	/// </summary>
	public interface IScreenService<TScreen> where TScreen : struct, Enum
	{
		/// <summary>The screen on top of the stack, or the default value when nothing is open.</summary>
		TScreen Current { get; }

		/// <summary>True while the screen is anywhere in the stack, covered or not.</summary>
		bool IsOpen(TScreen screen);

		UniTask<bool> Open(TScreen screen);

		/// <summary>Pops the top screen. The bottom-most screen is never popped.</summary>
		UniTask<bool> Close();
	}
}
