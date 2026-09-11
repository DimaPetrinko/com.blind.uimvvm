#nullable enable
using System;
using Cysharp.Threading.Tasks;

namespace Blind.UiMvvm.Navigation
{
	/// <summary>
	/// Owns one screen's lifetime. Each screen ships its own and registers it, so adding a screen never
	/// touches <see cref="IScreenService{TScreen}"/> or any caller.
	///
	/// The service drives all three calls: <see cref="Open"/> when the screen is pushed,
	/// <see cref="SetCovered"/> when another screen is pushed on top of or popped off it, and
	/// <see cref="Close"/> when it is popped. What those mean is the screen's business - a modal may
	/// instantiate and destroy itself, while a persistent screen may only show and hide.
	///
	/// <see cref="Open"/> and <see cref="Close"/> are awaitable so a screen can load what it needs
	/// before it claims to be open. An opener that has nothing to wait for should complete
	/// synchronously: the service awaits inline in that case, so the screen is ready within the same
	/// frame and never renders unbound.
	/// </summary>
	public interface IScreenOpener<TScreen> where TScreen : struct, Enum
	{
		TScreen Screen { get; }

		/// <summary>
		/// Shows the screen. Returns false when it declined to open - a stub, or a screen whose
		/// preconditions are not met - and the service then leaves the stack untouched, so nothing
		/// ends up hidden behind a screen that never appeared.
		/// </summary>
		UniTask<bool> Open();

		UniTask Close();

		/// <summary>Another screen is now on top of this one, or has just left.</summary>
		void SetCovered(bool covered);
	}
}
