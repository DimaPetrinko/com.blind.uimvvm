#nullable enable
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Transitions.Implementation
{
	/// <summary>
	/// Awaitable USS transitions for one element. Two things about UI Toolkit shape this, and both were
	/// found the hard way rather than read anywhere:
	///
	/// <b>A value that was never resolved has nothing to transition from.</b> Writing the start and end
	/// states in one frame snaps straight to the end and no transition is seen, so the start is written
	/// with transitions suppressed and the end is deferred by one panel tick.
	///
	/// <b>A transition that is interrupted reports a cancel, not an end.</b> A caller waiting on
	/// <c>TransitionEndEvent</c> never learns that a retargeted or superseded transition finished - and
	/// an element whose stylesheet declares no transition at all never fires either event, so the wait
	/// hangs forever. Completion is therefore a timer armed from the resolved duration, and this class
	/// registers no transition callbacks at all.
	///
	/// One instance owns one element. Two owners animating one element is a bug you can see, which is
	/// why this is not a set of extension methods over a static table.
	/// </summary>
	public sealed class UssTransition : IUssTransition
	{
		private readonly VisualElement mElement;
		private readonly ITransitionScheduler mScheduler;

		private IDisposable? mPendingSnapRestore;
		private Run? mRun;

		public UssTransition(VisualElement element, ITransitionScheduler? scheduler = null)
		{
			mElement = element ?? throw new ArgumentNullException(nameof(element));
			mScheduler = scheduler ?? new PanelTransitionScheduler(element);
		}

		public bool IsRunning => mRun != null;

		public UniTask<bool> Play(string className, bool enabled, float? durationSeconds = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(className))
				throw new ArgumentException("A class name is required", nameof(className));

			return Play(null, element => element.EnableInClassList(className, enabled), durationSeconds,
				cancellationToken);
		}

		public UniTask<bool> PlayFrom(string className, float? durationSeconds = null,
			CancellationToken cancellationToken = default)
		{
			if (string.IsNullOrEmpty(className))
				throw new ArgumentException("A class name is required", nameof(className));

			return Play(
				element => element.EnableInClassList(className, true),
				element => element.EnableInClassList(className, false),
				durationSeconds,
				cancellationToken);
		}

		public UniTask<bool> Play(Action<VisualElement>? from, Action<VisualElement> to,
			float? durationSeconds = null, CancellationToken cancellationToken = default)
		{
			if (to == null)
				throw new ArgumentNullException(nameof(to));

			Cancel();

			if (cancellationToken.IsCancellationRequested)
				return UniTask.FromResult(false);

			// No panel means no ticks will ever arrive, so a deferred write would never land and the
			// caller would wait forever. Apply the end state now and report it as reached.
			if (mElement.panel == null)
			{
				from?.Invoke(mElement);
				ApplyEnd(to, durationSeconds);
				return UniTask.FromResult(true);
			}

			var run = new Run(this, to, durationSeconds, cancellationToken);
			mRun = run;
			run.Start(from);
			return run.Task;
		}

		public void Snap(Action<VisualElement> write)
		{
			if (write == null)
				throw new ArgumentNullException(nameof(write));

			Cancel();

			SetInlineDuration(0f);
			write(mElement);

			// Restored a tick later, once the write has resolved at zero, so the element is left with
			// whatever its stylesheet says rather than pinned to 0s forever.
			if (mElement.panel == null)
				ClearInlineDuration();
			else
				mPendingSnapRestore = mScheduler.NextTick(ClearInlineDuration);
		}

		public void Cancel()
		{
			mPendingSnapRestore?.Dispose();
			mPendingSnapRestore = null;

			mRun?.Complete(false);
		}

		private void ApplyEnd(Action<VisualElement> to, float? durationSeconds)
		{
			if (durationSeconds.HasValue)
				SetInlineDuration(durationSeconds.Value);
			else
				ClearInlineDuration();

			to(mElement);
		}

		private void SetInlineDuration(float seconds) =>
			mElement.style.transitionDuration =
				new StyleList<TimeValue>(new List<TimeValue> { new(seconds, TimeUnit.Second) });

		/// <summary>
		/// <see cref="StyleKeyword.Null"/> removes the inline value rather than setting one, handing the
		/// property back to the stylesheet. Anything else leaves the element pinned to whatever this
		/// class last wrote, and every later transition on it silently runs at that duration.
		/// </summary>
		private void ClearInlineDuration() =>
			mElement.style.transitionDuration = new StyleList<TimeValue>(StyleKeyword.Null);

		private void Release(Run run)
		{
			if (ReferenceEquals(mRun, run))
				mRun = null;
		}

		private float ResolveEndSeconds()
		{
			var resolved = mElement.resolvedStyle;
			return LongestOf(resolved.transitionDelay, resolved.transitionDuration);
		}

		/// <summary>
		/// The transition is over when its slowest property is: the largest delay + duration pair, with
		/// the shorter list cycled against the longer one the way CSS does.
		/// </summary>
		private static float LongestOf(IEnumerable<TimeValue>? delays, IEnumerable<TimeValue>? durations)
		{
			var durationList = durations == null ? Array.Empty<TimeValue>() : durations.ToArray();
			if (durationList.Length == 0)
				return 0f;

			var delayList = delays == null ? Array.Empty<TimeValue>() : delays.ToArray();
			var steps = Math.Max(durationList.Length, delayList.Length);
			var longest = 0f;

			for (var i = 0; i < steps; i++)
			{
				var duration = ToSeconds(durationList[i % durationList.Length]);
				var delay = delayList.Length == 0 ? 0f : ToSeconds(delayList[i % delayList.Length]);
				var end = delay + duration;

				if (end > longest)
					longest = end;
			}

			return longest;
		}

		private static float ToSeconds(TimeValue time) =>
			time.unit == TimeUnit.Millisecond ? time.value / 1000f : time.value;

		private sealed class Run
		{
			private readonly UssTransition mOwner;
			private readonly Action<VisualElement> mTo;
			private readonly float? mDurationSeconds;
			private readonly UniTaskCompletionSource<bool> mSource = new();
			private readonly EventCallback<DetachFromPanelEvent> mOnDetach;

			private CancellationTokenRegistration mCancellation;
			private IDisposable? mPending;
			private bool mHasWrittenEnd;
			private bool mIsComplete;

			public Run(UssTransition owner, Action<VisualElement> to, float? durationSeconds,
				CancellationToken cancellationToken)
			{
				mOwner = owner;
				mTo = to;
				mDurationSeconds = durationSeconds;
				mOnDetach = _ => OnDetached();

				mOwner.mElement.RegisterCallback(mOnDetach);
				mCancellation = cancellationToken.Register(() => Complete(false));
			}

			public UniTask<bool> Task => mSource.Task;

			public void Start(Action<VisualElement>? from)
			{
				if (from == null)
				{
					WriteEnd();
					return;
				}

				mOwner.SetInlineDuration(0f);
				from(mOwner.mElement);

				// The start state has to be resolved before the end state is written, or there is
				// nothing to transition from.
				Schedule(mOwner.mScheduler.NextTick(WriteEnd));
			}

			public void Complete(bool reachedEnd)
			{
				if (mIsComplete)
					return;

				mIsComplete = true;

				mPending?.Dispose();
				mPending = null;
				mCancellation.Dispose();
				mOwner.mElement.UnregisterCallback(mOnDetach);
				mOwner.Release(this);

				mSource.TrySetResult(reachedEnd);
			}

			private void WriteEnd()
			{
				if (mIsComplete)
					return;

				mOwner.ApplyEnd(mTo, mDurationSeconds);
				mHasWrittenEnd = true;

				if (mDurationSeconds.HasValue)
				{
					Arm(mDurationSeconds.Value);
					return;
				}

				// resolvedStyle lags the write by a style pass, so reading it now would still report the
				// zero written for the start state. One more tick, then read.
				Schedule(mOwner.mScheduler.NextTick(() => ReadAndArm(true)));
			}

			private void ReadAndArm(bool mayRetry)
			{
				if (mIsComplete)
					return;

				var end = mOwner.ResolveEndSeconds();
				if (end > 0f)
				{
					Arm(end);
					return;
				}

				// Zero means either "no transition declared" or "not resolved yet", and the two are not
				// distinguishable from here. One retry settles it; an event-based wait would simply hang
				// in the first case.
				if (mayRetry)
				{
					Schedule(mOwner.mScheduler.NextTick(() => ReadAndArm(false)));
					return;
				}

				Complete(true);
			}

			private void Arm(float seconds) => Schedule(mOwner.mScheduler.After(seconds, () => Complete(true)));

			private void Schedule(IDisposable handle)
			{
				mPending?.Dispose();
				mPending = handle;
			}

			/// <summary>
			/// The panel is gone, so nothing will tick again. The end state is still what the caller
			/// asked for - a screen torn down mid-fade must not report that the fade never happened.
			/// </summary>
			private void OnDetached()
			{
				if (mIsComplete)
					return;

				if (!mHasWrittenEnd)
					mOwner.ApplyEnd(mTo, mDurationSeconds);

				Complete(true);
			}
		}
	}
}
