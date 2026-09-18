#nullable enable
using System;
using System.Collections.Generic;
using System.Reflection;
using Blind.Reactive.Implementation;
using Blind.UiMvvm.Views;
using NUnit.Framework;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Tests
{
	/// <summary>
	/// The helpers a view uses to wire itself up, and the teardown that undoes them. Nothing here needs
	/// a running player: an EditMode UIDocument builds a real panel.
	/// </summary>
	public class ViewLifecycleTests
	{
		private const string FixturePath = "Packages/com.blind.uimvvm/Tests/Fixtures/CounterPanel.uxml";
		private const string RootName = "CounterPanel";

		private GameObject mHost = null!;
		private readonly List<ScriptableObject> mCreatedAssets = new();

		[TearDown]
		public void TearDown()
		{
			if (mHost != null)
				UnityEngine.Object.DestroyImmediate(mHost);

			foreach (var asset in mCreatedAssets)
				if (asset != null)
					UnityEngine.Object.DestroyImmediate(asset);

			mCreatedAssets.Clear();
		}

		[Test]
		public void Require_ReturnsTheNamedElement()
		{
			var view = CreateView();
			view.Bind(new CounterViewModel());

			Assert.That(view.RequireButton("Reset"), Is.Not.Null);
		}

		/// <summary>
		/// Q returns null for a renamed element, and a null that far from its cause is the hardest kind
		/// of UI bug to place.
		/// </summary>
		[Test]
		public void Require_ThrowsNamingTheViewTheElementAndTheRoot()
		{
			var view = CreateView();
			view.Bind(new CounterViewModel());

			var exception = Assert.Throws<MissingComponentException>(() => view.RequireButton("NoSuchButton"));

			Assert.That(exception!.Message, Does.Contain("NoSuchButton"));
			Assert.That(exception.Message, Does.Contain(RootName));
		}

		[Test]
		public void Require_BeforeBinding_SaysSo()
		{
			var view = CreateView();

			var exception = Assert.Throws<InvalidOperationException>(() => view.RequireButton("Reset"));

			Assert.That(exception!.Message, Does.Contain("not bound"));
		}

		[Test]
		public void Optional_ReturnsNullForAMissingElement()
		{
			var view = CreateView();
			view.Bind(new CounterViewModel());

			Assert.That(view.OptionalButton("NoSuchButton"), Is.Null);
		}

		[Test]
		public void OnClick_WiresTheButton()
		{
			var view = CreateView();
			var viewModel = new CounterViewModel();
			view.Bind(viewModel);

			Click(view.RequireButton("Reset"));

			Assert.That(viewModel.ResetClickCount, Is.EqualTo(1));
		}

		[Test]
		public void Unbind_UnwiresEveryButtonItWired()
		{
			var view = CreateView();
			var viewModel = new CounterViewModel();
			view.Bind(viewModel);
			var button = view.RequireButton("Reset");

			view.Unbind();
			Click(button);

			Assert.That(viewModel.ResetClickCount, Is.Zero);
		}

		[Test]
		public void Unbind_DisposesEverythingTracked_AndClearsTheDataSource()
		{
			var view = CreateView();
			var viewModel = new CounterViewModel();
			var root = view.Bind(viewModel);
			var disposed = false;
			view.TrackThis(new Spy(() => disposed = true));

			view.Unbind();

			Assert.That(disposed, Is.True);
			Assert.That(root.dataSource, Is.Null);
			Assert.That(view.IsBound, Is.False);
		}

		[Test]
		public void Unbind_StopsAReactiveSubscription()
		{
			var view = CreateView();
			var source = new ReactiveProperty<int>();
			view.Bind(new CounterViewModel());
			view.SubscribeTo(source);

			source.Value = 1;
			view.Unbind();
			source.Value = 2;

			Assert.That(view.Seen, Is.EqualTo(new[] { 0, 1 }));
		}

		[Test]
		public void Unbind_Twice_IsSafe()
		{
			var view = CreateView();
			view.Bind(new CounterViewModel());

			view.Unbind();

			Assert.DoesNotThrow(() => view.Unbind());
		}

		[Test]
		public void Unbind_BeforeAnyBind_IsSafe()
		{
			var view = CreateView();

			Assert.DoesNotThrow(() => view.Unbind());
		}

		[Test]
		public void AViewCanBeBoundAgain_AfterUnbinding()
		{
			var view = CreateView();
			var first = new CounterViewModel();
			var second = new CounterViewModel();

			view.Bind(first);
			view.Unbind();
			var root = view.Bind(second);

			Assert.That(root.dataSource, Is.SameAs(second));

			Click(view.RequireButton("Reset"));

			Assert.That(second.ResetClickCount, Is.EqualTo(1));
			Assert.That(first.ResetClickCount, Is.Zero, "the first view model was still wired");
		}

		[Test]
		public void BindingTwiceWithoutUnbinding_DoesNotLeaveTheFirstWiringInPlace()
		{
			var view = CreateView();
			var first = new CounterViewModel();
			var second = new CounterViewModel();

			view.Bind(first);
			view.Bind(second);

			Click(view.RequireButton("Reset"));

			Assert.That(second.ResetClickCount, Is.EqualTo(1));
			Assert.That(first.ResetClickCount, Is.Zero);
		}

		/// <summary>
		/// A UIDocument rebuilds its visual tree when it is re-enabled, so everything the view cached
		/// belongs to a tree that is no longer on screen.
		/// </summary>
		[Test]
		public void ResolveRoot_LooksUpAgain_WhenTheCachedElementHasLeftItsPanel()
		{
			var view = CreateView();
			var document = mHost.GetComponent<UIDocument>();
			var first = view.ResolveRoot();

			document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FixturePath);

			var second = view.ResolveRoot();

			Assert.That(second, Is.Not.Null);
			Assert.That(second.panel, Is.Not.Null, "the view kept a root from a tree that is gone");
		}

		[Test]
		public void InvalidateRoot_ForcesAFreshLookup()
		{
			var view = CreateView();
			var first = view.ResolveRoot();

			view.InvalidateRoot();

			Assert.That(view.ResolveRoot(), Is.SameAs(first), "the same tree must still resolve the same element");
		}

		private static void Click(Button button)
		{
			using var evt = NavigationSubmitEvent.GetPooled();
			evt.target = button;
			button.SendEvent(evt);
		}

		private TestView CreateView()
		{
			mHost = new GameObject("ViewHost");
			var document = mHost.AddComponent<UIDocument>();

			var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();
			panelSettings.themeStyleSheet = theme;
			mCreatedAssets.Add(panelSettings);
			mCreatedAssets.Add(theme);

			document.panelSettings = panelSettings;
			document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FixturePath);

			var view = mHost.AddComponent<TestView>();
			typeof(ViewRoot)
				.GetField("m_RootName", BindingFlags.Instance | BindingFlags.NonPublic)!
				.SetValue(view, RootName);

			return view;
		}

		private class TestView : View<CounterViewModel>
		{
			public List<int> Seen { get; } = new();

			public new VisualElement Bind(CounterViewModel viewModel)
			{
				base.Bind(viewModel);
				OnClick("Reset", mViewModel.OnResetClicked);
				return mRoot;
			}

			public Button RequireButton(string name) => Require<Button>(name);

			public Button? OptionalButton(string name) => Optional<Button>(name);

			public void TrackThis(IDisposable disposable) => Track(disposable);

			public void SubscribeTo(ReactiveProperty<int> property) => Subscribe(property, Seen.Add);
		}

		private class CounterViewModel
		{
			[CreateProperty] public string Counter { get; private set; } = "0";

			public int ResetClickCount { get; private set; }

			public void OnResetClicked() => ResetClickCount++;
		}

		private class Spy : IDisposable
		{
			private readonly Action mOnDispose;

			public Spy(Action onDispose) => mOnDispose = onDispose;

			public void Dispose() => mOnDispose();
		}
	}
}
