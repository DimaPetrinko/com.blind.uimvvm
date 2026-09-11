#nullable enable
using System;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Transitions
{
	/// <summary>
	/// Drives one element's USS transitions and tells the caller when they are done.
	///
	/// The returned task <b>always completes exactly once and never throws</b> - not even
	/// <see cref="OperationCanceledException"/>. These are awaited inside screen openers whose tasks
	/// callers routinely drop with <c>Forget()</c>, where an escaping exception would turn a cancelled
	/// fade into an unhandled-exception log and a half-finished navigation.
	///
	/// <c>true</c> means the end state is on screen, so <c>await fade; Destroy();</c> is safe without
	/// the caller having to know whether the panel survived. <c>false</c> means something else took the
	/// element over - a newer play, <see cref="Cancel"/>, or a cancellation token.
	/// </summary>
	public interface IUssTransition
	{
		bool IsRunning { get; }

		/// <summary>Adds or removes a class and completes when the transition it starts ends.</summary>
		UniTask<bool> Play(string className, bool enabled, float? durationSeconds = null,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// Uses the class as a start state: applies it with transitions suppressed, lets the panel
		/// resolve it, then removes it. The enter animation.
		/// </summary>
		UniTask<bool> PlayFrom(string className, float? durationSeconds = null,
			CancellationToken cancellationToken = default);

		/// <summary>
		/// The general form. <paramref name="from"/> is written with transitions suppressed and given a
		/// frame to resolve before <paramref name="to"/> lands; pass null when the element already shows
		/// its start state.
		/// </summary>
		UniTask<bool> Play(Action<VisualElement>? from, Action<VisualElement> to, float? durationSeconds = null,
			CancellationToken cancellationToken = default);

		/// <summary>Writes with transitions suppressed and no wait. Cancels anything running.</summary>
		void Snap(Action<VisualElement> write);

		/// <summary>Stops the running transition where it is; its task completes false.</summary>
		void Cancel();
	}

	/// <summary>
	/// Where a transition gets its frames. Exists so the one-frame deferral and the completion timer can
	/// be asserted without a running player; callers never name it.
	/// </summary>
	public interface ITransitionScheduler
	{
		IDisposable NextTick(Action action);

		IDisposable After(float seconds, Action action);
	}
}
