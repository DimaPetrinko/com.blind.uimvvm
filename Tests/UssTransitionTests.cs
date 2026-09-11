#nullable enable
using System;
using System.Collections.Generic;
using System.Threading;
using Blind.UiMvvm.Transitions;
using Blind.UiMvvm.Transitions.Implementation;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Tests
{
	/// <summary>
	/// Driven through a fake scheduler, so every frame boundary is an explicit call and nothing depends
	/// on a running player.
	/// </summary>
	public class UssTransitionTests
	{
		private GameObject mHost = null!;
		private PanelSettings mPanelSettings = null!;
		private ThemeStyleSheet mTheme = null!;
		private VisualElement mElement = null!;
		private FakeScheduler mScheduler = null!;

		[SetUp]
		public void SetUp()
		{
			mHost = new GameObject("TransitionHost");
			var document = mHost.AddComponent<UIDocument>();

			mPanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			mTheme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
			mPanelSettings.themeStyleSheet = mTheme;
			document.panelSettings = mPanelSettings;

			mElement = new VisualElement();
			document.rootVisualElement.Add(mElement);

			mScheduler = new FakeScheduler();
		}

		[TearDown]
		public void TearDown()
		{
			UnityEngine.Object.DestroyImmediate(mHost);
			UnityEngine.Object.DestroyImmediate(mPanelSettings);
			UnityEngine.Object.DestroyImmediate(mTheme);
		}

		/// <summary>
		/// Trap one: a value that was never resolved has nothing to transition from, so the end state
		/// must not land in the same frame as the start state.
		/// </summary>
		[Test]
		public void Play_WritesTheStartStateNow_AndTheEndStateOnTheNextTick()
		{
			var transition = new UssTransition(mElement, mScheduler);

			transition.Play(
				element => element.AddToClassList("from"),
				element => element.AddToClassList("to"),
				0.2f).Forget();

			Assert.That(mElement.ClassListContains("from"), Is.True);
			Assert.That(mElement.ClassListContains("to"), Is.False, "the end state landed in the same frame");

			mScheduler.Tick();

			Assert.That(mElement.ClassListContains("to"), Is.True);
		}

		[Test]
		public void Play_SuppressesTheTransitionForTheStartState_AndRestoresItForTheEnd()
		{
			var transition = new UssTransition(mElement, mScheduler);

			transition.Play(element => element.AddToClassList("from"), element => element.AddToClassList("to"), 0.2f)
				.Forget();

			Assert.That(InlineDurationSeconds(), Is.EqualTo(0f));

			mScheduler.Tick();

			Assert.That(InlineDurationSeconds(), Is.EqualTo(0.2f));
		}

		[Test]
		public void Play_WithAnExplicitDuration_CompletesOnceThatDurationHasElapsed()
		{
			var transition = new UssTransition(mElement, mScheduler);
			var task = transition.Play(null, element => element.AddToClassList("to"), 0.25f);

			Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));

			mScheduler.Advance(0.24f);
			Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Pending));

			mScheduler.Advance(0.01f);

			Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
			Assert.That(task.GetAwaiter().GetResult(), Is.True);
		}

		/// <summary>
		/// Trap two, the half that matters most: an element whose stylesheet declares no transition
		/// never fires a transition event at all, so an event-based wait would hang forever.
		/// </summary>
		[Test]
		public void Play_WithNoTransitionDeclared_StillCompletes()
		{
			var transition = new UssTransition(mElement, mScheduler);
			var task = transition.Play("shown", true);

			// One tick to read the resolved style, one more for the retry that tells "not resolved yet"
			// apart from "nothing declared".
			mScheduler.Tick();
			mScheduler.Tick();

			Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
			Assert.That(task.GetAwaiter().GetResult(), Is.True);
			Assert.That(mElement.ClassListContains("shown"), Is.True);
		}

		[Test]
		public void ASecondPlay_SupersedesTheFirst_WhichReportsFalse()
		{
			var transition = new UssTransition(mElement, mScheduler);
			var first = transition.Play(null, element => element.AddToClassList("first"), 1f);

			var second = transition.Play(null, element => element.AddToClassList("second"), 1f);

			Assert.That(first.GetAwaiter().GetResult(), Is.False);
			Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending));

			mScheduler.Advance(1f);

			Assert.That(second.GetAwaiter().GetResult(), Is.True);
		}

		[Test]
		public void AStaleTimerFromASupersededRun_DoesNotCompleteTheNewOne()
		{
			var transition = new UssTransition(mElement, mScheduler);
			transition.Play(null, element => element.AddToClassList("first"), 0.1f).Forget();

			var second = transition.Play(null, element => element.AddToClassList("second"), 1f);

			mScheduler.Advance(0.1f);

			Assert.That(second.Status, Is.EqualTo(UniTaskStatus.Pending), "the first run's timer completed the second");
		}

		[Test]
		public void Cancel_CompletesFalse_AndLeavesTheEndStateUnapplied()
		{
			var transition = new UssTransition(mElement, mScheduler);
			var task = transition.Play(
				element => element.AddToClassList("from"),
				element => element.AddToClassList("to"),
				1f);

			transition.Cancel();

			Assert.That(task.GetAwaiter().GetResult(), Is.False);
			Assert.That(mElement.ClassListContains("to"), Is.False);
			Assert.That(transition.IsRunning, Is.False);
		}

		[Test]
		public void Cancel_WithNothingRunning_IsSafe()
		{
			var transition = new UssTransition(mElement, mScheduler);

			Assert.DoesNotThrow(() => transition.Cancel());
		}

		[Test]
		public void AnAlreadyCancelledToken_CompletesFalse_AndWritesNothing()
		{
			var transition = new UssTransition(mElement, mScheduler);
			using var source = new CancellationTokenSource();
			source.Cancel();

			var task = transition.Play("shown", true, null, source.Token);

			Assert.That(task.GetAwaiter().GetResult(), Is.False);
			Assert.That(mElement.ClassListContains("shown"), Is.False);
		}

		[Test]
		public void ATokenCancelledMidRun_CompletesFalse()
		{
			var transition = new UssTransition(mElement, mScheduler);
			using var source = new CancellationTokenSource();
			var task = transition.Play(null, element => element.AddToClassList("to"), 1f, source.Token);

			source.Cancel();

			Assert.That(task.GetAwaiter().GetResult(), Is.False);
		}

		/// <summary>
		/// The screen was torn down mid-fade. The end state is still what the caller asked for, so
		/// reporting that the fade never happened would strand whatever was waiting on it.
		/// </summary>
		[Test]
		public void DetachingFromThePanel_CompletesTrue_AndAppliesTheEndState()
		{
			var transition = new UssTransition(mElement, mScheduler);
			var task = transition.Play(
				element => element.AddToClassList("from"),
				element => element.AddToClassList("to"),
				1f);

			mElement.RemoveFromHierarchy();

			Assert.That(task.GetAwaiter().GetResult(), Is.True);
			Assert.That(mElement.ClassListContains("to"), Is.True);
		}

		[Test]
		public void PlayOnAnElementWithNoPanel_CompletesImmediately_WithTheEndStateApplied()
		{
			var orphan = new VisualElement();
			var transition = new UssTransition(orphan, mScheduler);

			var task = transition.Play("shown", true);

			Assert.That(task.Status, Is.EqualTo(UniTaskStatus.Succeeded));
			Assert.That(task.GetAwaiter().GetResult(), Is.True);
			Assert.That(orphan.ClassListContains("shown"), Is.True);
		}

		[Test]
		public void PlayFrom_AppliesTheClassAsAStartState_AndRemovesItAsTheEnd()
		{
			var transition = new UssTransition(mElement, mScheduler);

			transition.PlayFrom("hidden", 0.2f).Forget();

			Assert.That(mElement.ClassListContains("hidden"), Is.True);

			mScheduler.Tick();

			Assert.That(mElement.ClassListContains("hidden"), Is.False);
		}

		[Test]
		public void Snap_CancelsWhatIsRunning_AndWritesWithTheTransitionSuppressed()
		{
			var transition = new UssTransition(mElement, mScheduler);
			var running = transition.Play(null, element => element.AddToClassList("to"), 1f);

			transition.Snap(element => element.AddToClassList("snapped"));

			Assert.That(running.GetAwaiter().GetResult(), Is.False);
			Assert.That(mElement.ClassListContains("snapped"), Is.True);
			Assert.That(InlineDurationSeconds(), Is.EqualTo(0f));
		}

		private float InlineDurationSeconds()
		{
			var durations = mElement.style.transitionDuration.value;
			return durations == null || durations.Count == 0 ? -1f : durations[0].value;
		}

		private class FakeScheduler : ITransitionScheduler
		{
			// Advancing in steps accumulates error, so a timer armed for exactly the total would
			// otherwise miss its own deadline by a few nanoseconds.
			private const float Epsilon = 1e-5f;

			private readonly List<Entry> mEntries = new();

			public IDisposable NextTick(Action action) => Add(0f, action);

			public IDisposable After(float seconds, Action action) => Add(seconds, action);

			/// <summary>One panel tick: everything scheduled for the next frame runs.</summary>
			public void Tick() => Advance(0f);

			public void Advance(float seconds)
			{
				var due = new List<Entry>();

				foreach (var entry in mEntries)
				{
					entry.Remaining -= seconds;
					if (entry.Remaining <= Epsilon)
						due.Add(entry);
				}

				// Snapshot first: a callback normally schedules the next step, and that one belongs to
				// the following tick.
				foreach (var entry in due)
				{
					mEntries.Remove(entry);

					if (!entry.IsCancelled)
						entry.Action();
				}
			}

			private IDisposable Add(float seconds, Action action)
			{
				var entry = new Entry { Remaining = seconds, Action = action };
				mEntries.Add(entry);
				return entry;
			}

			private class Entry : IDisposable
			{
				public float Remaining;
				public Action Action = null!;
				public bool IsCancelled { get; private set; }

				public void Dispose() => IsCancelled = true;
			}
		}
	}
}
