using System.Collections.Generic;
using System.Linq;
using Cysharp.Threading.Tasks;
using Blind.UiMvvm.Navigation;
using Blind.UiMvvm.Navigation.Implementation;
using NUnit.Framework;

namespace Blind.UiMvvm.Tests
{
	/// <summary>The screen set is the consuming project's; these stand in for one.</summary>
	internal enum TestScreen
	{
		None = 0,
		Hub = 1,
		Shop = 2,
		Progress = 3,
		Settings = 4,
		Modal = 5,
	}

	internal class ScreenServiceTests
	{
		private class RecordingOpener : IScreenOpener<TestScreen>
		{
			public RecordingOpener(TestScreen screen) => Screen = screen;

			public TestScreen Screen { get; }
			public int OpenCount { get; private set; }
			public int CloseCount { get; private set; }
			public bool IsCovered { get; private set; }
			public bool RefusesToOpen { get; set; }

			public UniTask<bool> Open()
			{
				if (RefusesToOpen)
					return UniTask.FromResult(false);

				OpenCount++;
				return UniTask.FromResult(true);
			}

			public UniTask Close()
			{
				CloseCount++;
				return UniTask.CompletedTask;
			}

			public void SetCovered(bool covered) => IsCovered = covered;
		}

		private class GatedOpener : IScreenOpener<TestScreen>
		{
			private readonly UniTaskCompletionSource<bool> _gate = new();

			public GatedOpener(TestScreen screen) => Screen = screen;

			public TestScreen Screen { get; }
			public int OpenCount { get; private set; }

			public UniTask<bool> Open()
			{
				OpenCount++;
				return _gate.Task;
			}

			public void Release(bool opened) => _gate.TrySetResult(opened);

			public UniTask Close() => UniTask.CompletedTask;

			public void SetCovered(bool covered)
			{
			}
		}


		private class GatedCloseOpener : IScreenOpener<TestScreen>
		{
			private readonly UniTaskCompletionSource _gate = new();

			public GatedCloseOpener(TestScreen screen) => Screen = screen;

			public TestScreen Screen { get; }
			public bool IsCovered { get; private set; }

			public UniTask<bool> Open() => UniTask.FromResult(true);

			public UniTask Close() => _gate.Task;

			public void Release() => _gate.TrySetResult();

			public void SetCovered(bool covered) => IsCovered = covered;
		}

		private readonly List<string> _warnings = new();
		private readonly List<string> _errors = new();

		private ScreenService<TestScreen> Create(params IScreenOpener<TestScreen>[] openers) =>
			new(new List<IScreenOpener<TestScreen>>(openers), _warnings.Add, _errors.Add);

		private static bool Run(UniTask<bool> transition) => transition.GetAwaiter().GetResult();

		[SetUp]
		public void SetUp()
		{
			_warnings.Clear();
			_errors.Clear();
		}

		[Test]
		public void Open_RoutesToTheOpenerForThatScreen()
		{
			var shop = new RecordingOpener(TestScreen.Shop);
			var settings = new RecordingOpener(TestScreen.Settings);
			var service = Create(shop, settings);

			Run(service.Open(TestScreen.Shop));

			Assert.That(shop.OpenCount, Is.EqualTo(1));
			Assert.That(settings.OpenCount, Is.Zero);
		}

		[Test]
		public void Open_IsSafe_WhenNoOpenerIsRegistered()
		{
			var service = Create();

			Assert.DoesNotThrow(() => service.Open(TestScreen.Hub));
			Assert.That(_warnings, Has.Count.EqualTo(1));
			Assert.That(_warnings[0], Does.Contain("Hub"));
		}

		/// <summary>
		/// The reason None owns the zero value: a default or unset TestScreen must never reach a real
		/// screen's opener.
		/// </summary>
		[Test]
		public void Open_WithADefaultTestScreen_ReachesNoOpener()
		{
			var openers = new[]
			{
				new RecordingOpener(TestScreen.Hub),
				new RecordingOpener(TestScreen.Shop),
				new RecordingOpener(TestScreen.Progress),
				new RecordingOpener(TestScreen.Settings),
				new RecordingOpener(TestScreen.Modal),
			};
			var service = Create(openers);

			Run(service.Open(default));

			Assert.That(default(TestScreen), Is.EqualTo(TestScreen.None));
			foreach (var opener in openers)
				Assert.That(opener.OpenCount, Is.Zero, $"{opener.Screen} was opened by a default TestScreen");

			Assert.That(_errors, Has.Count.EqualTo(1));
			Assert.That(_errors[0], Does.Contain("does not name a screen"));
		}

		[Test]
		public void Open_CoversTheScreenBeneath_AndCloseUncoversIt()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var weapon = new RecordingOpener(TestScreen.Modal);
			var service = Create(hub, weapon);
			Run(service.Open(TestScreen.Hub));

			Run(service.Open(TestScreen.Modal));

			Assert.That(hub.IsCovered, Is.True);
			Assert.That(service.Current, Is.EqualTo(TestScreen.Modal));

			Run(service.Close());

			Assert.That(hub.IsCovered, Is.False);
			Assert.That(weapon.CloseCount, Is.EqualTo(1));
			Assert.That(service.Current, Is.EqualTo(TestScreen.Hub));
		}

		/// <summary>
		/// A stub declines to open. If the stack accepted it anyway, the hub would be hidden behind a
		/// screen that never appeared and nothing could bring it back.
		/// </summary>
		[Test]
		public void AnOpenerThatDeclines_LeavesTheStackUntouched()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var shop = new RecordingOpener(TestScreen.Shop) { RefusesToOpen = true };
			var service = Create(hub, shop);
			Run(service.Open(TestScreen.Hub));

			Run(service.Open(TestScreen.Shop));

			Assert.That(hub.IsCovered, Is.False, "the hub was hidden behind a screen that never opened");
			Assert.That(service.Current, Is.EqualTo(TestScreen.Hub));
		}

		[Test]
		public void TheRootScreen_IsNeverClosed()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var service = Create(hub);
			Run(service.Open(TestScreen.Hub));

			Run(service.Close());

			Assert.That(hub.CloseCount, Is.Zero);
			Assert.That(service.Current, Is.EqualTo(TestScreen.Hub));
			Assert.That(_warnings, Has.Count.EqualTo(1));
			Assert.That(_warnings[0], Does.Contain("root screen"));
		}

		[Test]
		public void OpeningTheScreenThatIsAlreadyOnTop_IsIgnored()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var service = Create(hub);
			Run(service.Open(TestScreen.Hub));

			Run(service.Open(TestScreen.Hub));

			Assert.That(hub.OpenCount, Is.EqualTo(1));
		}

		[Test]
		public void Close_WithNothingOpen_IsSafe()
		{
			var service = Create(new RecordingOpener(TestScreen.Hub));

			Assert.DoesNotThrow(() => service.Close());
			Assert.That(service.Current, Is.EqualTo(TestScreen.None));
		}

		[Test]
		public void AnOpenerDeclaringNone_IsRejectedAtConstruction()
		{
			Create(new RecordingOpener(TestScreen.None));

			Assert.That(_errors, Has.Count.EqualTo(1));
			Assert.That(_errors[0], Does.Contain("ignoring it"));
		}

		[Test]
		public void AnOpenerDeclaringNone_IsNeverDispatchedTo()
		{
			var stray = new RecordingOpener(TestScreen.None);
			var service = Create(stray);

			Run(service.Open(TestScreen.None));

			Assert.That(stray.OpenCount, Is.Zero);
		}

		[Test]
		public void TheLastOpenerWins_WhenTwoClaimTheSameScreen()
		{
			var first = new RecordingOpener(TestScreen.Shop);
			var second = new RecordingOpener(TestScreen.Shop);
			var service = Create(first, second);

			Run(service.Open(TestScreen.Shop));

			Assert.That(second.OpenCount, Is.EqualTo(1));
			Assert.That(first.OpenCount, Is.Zero);
			Assert.That(_warnings, Has.Count.EqualTo(1));
			Assert.That(_warnings[0], Does.Contain("More than one opener"));
		}

		/// <summary>
		/// An awaited open has not happened yet. The stack must not move until the opener says it is
		/// open, or the screen beneath would be covered before anything replaced it.
		/// </summary>
		[Test]
		public void AnOpenerThatAwaits_PushesOnlyOnceItCompletes()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var weapon = new GatedOpener(TestScreen.Modal);
			var service = Create(hub, weapon);
			Run(service.Open(TestScreen.Hub));

			service.Open(TestScreen.Modal).Forget();

			Assert.That(weapon.OpenCount, Is.EqualTo(1));
			Assert.That(service.Current, Is.EqualTo(TestScreen.Hub));
			Assert.That(hub.IsCovered, Is.False);

			weapon.Release(true);

			Assert.That(service.Current, Is.EqualTo(TestScreen.Modal));
			Assert.That(hub.IsCovered, Is.True);
		}

		[Test]
		public void ASecondTransition_IsRefusedWhileTheFirstIsStillRunning()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var weapon = new GatedOpener(TestScreen.Modal);
			var shop = new RecordingOpener(TestScreen.Shop);
			var service = Create(hub, weapon, shop);
			Run(service.Open(TestScreen.Hub));

			service.Open(TestScreen.Modal).Forget();
			service.Open(TestScreen.Shop).Forget();

			Assert.That(shop.OpenCount, Is.Zero, "a second transition ran while the first was in flight");
			Assert.That(_warnings.Any(warning => warning.Contains("transition")), Is.True);

			weapon.Release(true);

			Assert.That(service.Current, Is.EqualTo(TestScreen.Modal));
		}

		/// <summary>
		/// One opener owns one screen. A second stack entry would hand it Open, SetCovered and Close
		/// for two slots at once, and the second close would tear down a screen still shown by the
		/// first.
		/// </summary>
		[Test]
		public void OpeningAScreenAlreadyLowerInTheStack_IsRefused()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var shop = new RecordingOpener(TestScreen.Shop);
			var service = Create(hub, shop);
			Run(service.Open(TestScreen.Hub));
			Run(service.Open(TestScreen.Shop));

			var opened = Run(service.Open(TestScreen.Hub));

			Assert.That(opened, Is.False);
			Assert.That(hub.OpenCount, Is.EqualTo(1));
			Assert.That(service.Current, Is.EqualTo(TestScreen.Shop));
			Assert.That(_warnings.Any(warning => warning.Contains("already open")), Is.True);
		}

		[Test]
		public void IsOpen_ReportsEveryScreenInTheStack_NotJustTheTop()
		{
			var service = Create(new RecordingOpener(TestScreen.Hub), new RecordingOpener(TestScreen.Shop));
			Run(service.Open(TestScreen.Hub));
			Run(service.Open(TestScreen.Shop));

			Assert.That(service.IsOpen(TestScreen.Hub), Is.True);
			Assert.That(service.IsOpen(TestScreen.Shop), Is.True);
			Assert.That(service.IsOpen(TestScreen.Settings), Is.False);
		}

		[Test]
		public void Open_ReportsWhetherTheTransitionHappened()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var declining = new RecordingOpener(TestScreen.Shop) { RefusesToOpen = true };
			var service = Create(hub, declining);

			Assert.That(Run(service.Open(TestScreen.Hub)), Is.True);
			Assert.That(Run(service.Open(TestScreen.Shop)), Is.False);
			Assert.That(Run(service.Open(TestScreen.Settings)), Is.False, "no opener is registered");
			Assert.That(Run(service.Close()), Is.False, "the root screen cannot be closed");
		}

		/// <summary>
		/// Current has to keep naming the screen the player is still looking at. Popping first makes it
		/// report the one underneath while an exit animation is still running on top of it.
		/// </summary>
		[Test]
		public void Close_KeepsCurrentOnTheClosingScreen_UntilItCompletes()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var modal = new GatedCloseOpener(TestScreen.Modal);
			var service = Create(hub, modal);
			Run(service.Open(TestScreen.Hub));
			Run(service.Open(TestScreen.Modal));

			service.Close().Forget();

			Assert.That(service.Current, Is.EqualTo(TestScreen.Modal), "popped before the close finished");

			modal.Release();

			Assert.That(service.Current, Is.EqualTo(TestScreen.Hub));
		}

		/// <summary>
		/// The screen below is revealed before the close is awaited, so an exit animation plays over it
		/// rather than over a collapsed screen showing nothing.
		/// </summary>
		[Test]
		public void Close_UncoversTheScreenBelow_BeforeAwaitingTheClose()
		{
			var hub = new RecordingOpener(TestScreen.Hub);
			var modal = new GatedCloseOpener(TestScreen.Modal);
			var service = Create(hub, modal);
			Run(service.Open(TestScreen.Hub));
			Run(service.Open(TestScreen.Modal));
			Assert.That(hub.IsCovered, Is.True);

			service.Close().Forget();

			Assert.That(hub.IsCovered, Is.False, "the hub was still hidden while the modal was leaving");

			modal.Release();

			Assert.That(hub.IsCovered, Is.False);
		}
	}
}
