#nullable enable
using System.Collections.Generic;
using Blind.UiMvvm.Binding;
using Blind.UiMvvm.Navigation;
using Blind.UiMvvm.Navigation.Implementation;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Tests
{
	public class ScreenOpenerTests
	{
		private readonly List<string> mWarnings = new();
		private readonly List<string> mErrors = new();

		[SetUp]
		public void SetUp()
		{
			mWarnings.Clear();
			mErrors.Clear();
		}

		/// <summary>
		/// The rule the whole opener shape exists to enforce: a screen is bound in the same call that
		/// created it, before anything can await, so it never renders its UXML placeholder values.
		/// </summary>
		[Test]
		public void Open_BindsTheScreen_ImmediatelyAfterCreatingIt()
		{
			var opener = new FakeModal(this);

			Run(opener.Open());

			Assert.That(opener.Calls, Is.EqualTo(new[] { "create", "bind", "created" }));
		}

		[Test]
		public void Open_CreatesOnce_ForAPersistentScreen()
		{
			var opener = new FakePersistent(this);

			Run(opener.Open());
			opener.SetCovered(true);
			Run(opener.Open());

			Assert.That(opener.CreateCount, Is.EqualTo(1));
			Assert.That(opener.BindCount, Is.EqualTo(1));
			Assert.That(opener.CurrentRoot!.style.display.value, Is.EqualTo(DisplayStyle.Flex));
		}

		[Test]
		public void Open_RefusesASecondOpen_ForAModalScreen()
		{
			var opener = new FakeModal(this);
			Run(opener.Open());

			var opened = Run(opener.Open());

			Assert.That(opened, Is.False);
			Assert.That(opener.CreateCount, Is.EqualTo(1));
			Assert.That(mWarnings, Has.Count.EqualTo(1));
		}

		[Test]
		public void Open_WhenCreationFails_ReportsFalse_AndTouchesNothing()
		{
			var opener = new FakePersistent(this) { CreationFails = true };

			var opened = Run(opener.Open());

			Assert.That(opened, Is.False);
			Assert.That(opener.BindCount, Is.Zero);
			Assert.That(mErrors, Has.Count.EqualTo(1));
		}

		/// <summary>A precondition that is simply not met is not an error, so nothing is logged.</summary>
		[Test]
		public void Open_WhenPrepareDeclines_CreatesNothing_AndLogsNothing()
		{
			var opener = new FakeModal(this) { PrepareResult = false };

			var opened = Run(opener.Open());

			Assert.That(opened, Is.False);
			Assert.That(opener.CreateCount, Is.Zero);
			Assert.That(mErrors, Is.Empty);
			Assert.That(mWarnings, Is.Empty);
		}

		/// <summary>
		/// Deliberate: refusing would leave a visible, unbound screen the stack does not know about and
		/// can therefore never close. A loud error and a wrong-looking screen is the diagnosable one.
		/// </summary>
		[Test]
		public void Open_WithNoBinder_StillOpens_ButReportsIt()
		{
			var opener = new FakeModal(this) { BinderIsMissing = true };

			var opened = Run(opener.Open());

			Assert.That(opened, Is.True);
			Assert.That(mErrors, Has.Count.EqualTo(1));
			Assert.That(mErrors[0], Does.Contain("placeholder"));
		}

		[Test]
		public void Close_DestroysAModal_AndTheNextOpenCreatesAFreshOne()
		{
			var opener = new FakeModal(this);
			Run(opener.Open());

			Run(opener.Close());

			Assert.That(opener.DestroyCount, Is.EqualTo(1));

			Run(opener.Open());

			Assert.That(opener.CreateCount, Is.EqualTo(2));
		}

		[Test]
		public void Close_WithNothingOpen_IsANoOp()
		{
			var opener = new FakeModal(this);

			Assert.DoesNotThrow(() => Run(opener.Close()));
			Assert.That(opener.DestroyCount, Is.Zero);
		}

		[Test]
		public void Close_CoversAPersistentScreen_AndKeepsIt()
		{
			var opener = new FakePersistent(this);
			Run(opener.Open());

			Run(opener.Close());

			Assert.That(opener.CurrentRoot!.style.display.value, Is.EqualTo(DisplayStyle.None));
			Assert.That(opener.CreateCount, Is.EqualTo(1));
		}

		[Test]
		public void SetCovered_BeforeAnyOpen_DoesNotThrow()
		{
			var opener = new FakePersistent(this);

			Assert.DoesNotThrow(() => opener.SetCovered(true));
		}

		/// <summary>
		/// A UIDocument rebuilds its visual tree whenever it is re-enabled, so a root held from an
		/// earlier open would be an orphan from the old tree.
		/// </summary>
		[Test]
		public void TheRoot_IsResolvedEveryTime_NotCached()
		{
			var opener = new FakePersistent(this);
			Run(opener.Open());
			var first = opener.CurrentRoot;

			opener.ReplaceRoot();
			opener.SetCovered(true);

			Assert.That(opener.CurrentRoot, Is.Not.SameAs(first));
			Assert.That(opener.CurrentRoot!.style.display.value, Is.EqualTo(DisplayStyle.None));
			Assert.That(first!.style.display.value, Is.EqualTo(DisplayStyle.Flex), "the stale root was styled");
		}

		[Test]
		public void PlayEnter_RunsAfterBinding_AndPlayExitBeforeDestroying()
		{
			var opener = new FakeModal(this) { Animates = true };

			Run(opener.Open());
			Run(opener.Close());

			Assert.That(opener.Calls, Is.EqualTo(new[] { "create", "bind", "created", "enter", "exit", "destroy" }));
		}

		[Test]
		public void ScreenCover_Display_CollapsesAndRestores()
		{
			var element = new VisualElement();
			var cover = ScreenCover.Display;

			cover.Apply(element, true);
			Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));

			cover.Apply(element, false);
			Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.Flex));
		}

		[Test]
		public void ScreenCover_UssClass_TogglesOnlyThatClass()
		{
			var element = new VisualElement();
			element.AddToClassList("keep-me");
			var cover = ScreenCover.UssClass("hidden");

			cover.Apply(element, true);
			Assert.That(element.ClassListContains("hidden"), Is.True);
			Assert.That(element.ClassListContains("keep-me"), Is.True);

			cover.Apply(element, false);
			Assert.That(element.ClassListContains("hidden"), Is.False);
		}

		[Test]
		public void ScreenCover_Default_BehavesAsDisplay()
		{
			var element = new VisualElement();

			default(ScreenCover).Apply(element, true);

			Assert.That(element.style.display.value, Is.EqualTo(DisplayStyle.None));
		}

		/// <summary>The payoff: a real service driving one persistent and one modal opener.</summary>
		[Test]
		public void AServiceDrivingBothOpeners_CoversTheHub_AndUncoversItOnClose()
		{
			var hub = new FakePersistent(this, TestScreen.Hub);
			var modal = new FakeModal(this, TestScreen.Modal);
			var service = new ScreenService<TestScreen>(
				new IScreenOpener<TestScreen>[] { hub, modal }, mWarnings.Add, mErrors.Add);

			Run(service.Open(TestScreen.Hub));
			Run(service.Open(TestScreen.Modal));

			Assert.That(hub.CurrentRoot!.style.display.value, Is.EqualTo(DisplayStyle.None));
			Assert.That(modal.CreateCount, Is.EqualTo(1));

			Run(service.Close());

			Assert.That(hub.CurrentRoot!.style.display.value, Is.EqualTo(DisplayStyle.Flex));
			Assert.That(modal.DestroyCount, Is.EqualTo(1));
			Assert.That(service.Current, Is.EqualTo(TestScreen.Hub));
		}

		private static bool Run(UniTask<bool> task) => task.GetAwaiter().GetResult();

		private static void Run(UniTask task) => task.GetAwaiter().GetResult();

		private class Screen
		{
			public VisualElement Root = new();
		}

		private class RecordingBinder : IViewBinder
		{
			private readonly List<string> mCalls;

			public RecordingBinder(List<string> calls) => mCalls = calls;

			public void Bind() => mCalls.Add("bind");
		}

		private class FakeModal : ModalScreenOpener<TestScreen, Screen>
		{
			public FakeModal(ScreenOpenerTests owner, TestScreen screen = TestScreen.Modal)
				: base(screen, owner.mWarnings.Add, owner.mErrors.Add)
			{
			}

			public List<string> Calls { get; } = new();
			public bool PrepareResult { get; set; } = true;
			public bool BinderIsMissing { get; set; }
			public bool Animates { get; set; }
			public int CreateCount { get; private set; }
			public int DestroyCount { get; private set; }

			protected override UniTask<bool> Prepare() => UniTask.FromResult(PrepareResult);

			protected override Screen CreateScreen()
			{
				CreateCount++;
				Calls.Add("create");
				return new Screen();
			}

			protected override IViewBinder? ResolveBinder(Screen instance) =>
				BinderIsMissing ? null : new RecordingBinder(Calls);

			protected override void OnScreenCreated(Screen instance) => Calls.Add("created");

			protected override UniTask PlayEnter()
			{
				if (Animates)
					Calls.Add("enter");

				return UniTask.CompletedTask;
			}

			protected override UniTask PlayExit()
			{
				if (Animates)
					Calls.Add("exit");

				return UniTask.CompletedTask;
			}

			protected override void DestroyScreen(Screen instance)
			{
				DestroyCount++;
				Calls.Add("destroy");
			}
		}

		private class FakePersistent : PersistentScreenOpener<TestScreen, Screen>
		{
			private Screen? mScreen;

			public FakePersistent(ScreenOpenerTests owner, TestScreen screen = TestScreen.Hub)
				: base(screen, ScreenCover.Display, owner.mWarnings.Add, owner.mErrors.Add)
			{
			}

			public bool CreationFails { get; set; }
			public int CreateCount { get; private set; }
			public int BindCount { get; private set; }

			public VisualElement? CurrentRoot => Root;

			public void ReplaceRoot() => mScreen!.Root = new VisualElement();

			protected override Screen? CreateScreen()
			{
				if (CreationFails)
					return null;

				CreateCount++;
				mScreen = new Screen();
				return mScreen;
			}

			protected override IViewBinder ResolveBinder(Screen instance)
			{
				BindCount++;
				return new RecordingBinder(new List<string>());
			}

			protected override VisualElement ResolveRoot(Screen instance) => instance.Root;
		}
	}
}
