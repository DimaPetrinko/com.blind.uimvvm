#nullable enable
using System.Collections.Generic;
using System.Reflection;
using Blind.UiMvvm.Views;
using NUnit.Framework;
using Unity.Properties;
using UnityEditor;
using UnityEngine;
using UnityEngine.UIElements;

namespace Blind.UiMvvm.Tests
{
	public class ViewTests
	{
		private const string FixturePath = "Packages/com.blind.uimvvm/Tests/Fixtures/CounterPanel.uxml";
		private const string NestedFixturePath = "Packages/com.blind.uimvvm/Tests/Fixtures/NestedPanels.uxml";
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

		private PanelSettings CreatePanelSettings()
		{
			var panelSettings = ScriptableObject.CreateInstance<PanelSettings>();
			var theme = ScriptableObject.CreateInstance<ThemeStyleSheet>();

			panelSettings.themeStyleSheet = theme;
			mCreatedAssets.Add(panelSettings);
			mCreatedAssets.Add(theme);

			return panelSettings;
		}

		[Test]
		public void Bind_ResolvesNamedRoot_AndAssignsDataSource()
		{
			var view = CreateView(RootName);
			var viewModel = new CounterViewModel();

			view.Bind(viewModel);

			Assert.That(view.Root, Is.Not.Null);
			Assert.That(view.Root.name, Is.EqualTo(RootName));
			Assert.That(view.Root.dataSource, Is.SameAs(viewModel));
		}

		[Test]
		public void Bind_ThrowsNamedException_WhenRootNameIsNotInTheTree()
		{
			var view = CreateView("NoSuchRoot");

			var exception = Assert.Throws<MissingComponentException>(
				() => view.Bind(new CounterViewModel()));

			Assert.That(exception!.Message, Does.Contain("NoSuchRoot"));
		}

		[Test]
		public void Bind_ThrowsNamedException_WhenUiDocumentIsMissing()
		{
			mHost = new GameObject("NoDocument");
			var view = mHost.AddComponent<CounterView>();

			var exception = Assert.Throws<MissingComponentException>(
				() => view.Bind(new CounterViewModel()));

			Assert.That(exception!.Message, Does.Contain("UIDocument"));
		}

		[Test]
		public void Bind_LetsTheViewWireElementsFromTheResolvedRoot()
		{
			var view = CreateView(RootName);
			var viewModel = new CounterViewModel();

			view.Bind(viewModel);

			Assert.That(view.ResetButton, Is.Not.Null);

			using (var evt = NavigationSubmitEvent.GetPooled())
			{
				evt.target = view.ResetButton;
				view.ResetButton!.SendEvent(evt);
			}

			Assert.That(viewModel.ResetClickCount, Is.EqualTo(1));
		}

		private CounterView CreateView(string rootName)
		{
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(FixturePath);
			Assert.That(visualTree, Is.Not.Null, $"could not load fixture at {FixturePath}");

			var panelSettings = CreatePanelSettings();

			mHost = new GameObject("UiHost");
			var document = mHost.AddComponent<UIDocument>();
			document.panelSettings = panelSettings;
			document.visualTreeAsset = visualTree;

			var view = mHost.AddComponent<CounterView>();
			SetRootName(view, rootName);
			return view;
		}

		private (CounterView First, CounterView Second) CreateSiblingViews(string firstName, string secondName)
		{
			var visualTree = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(NestedFixturePath);
			Assert.That(visualTree, Is.Not.Null, $"could not load fixture at {NestedFixturePath}");

			var panelSettings = CreatePanelSettings();

			mHost = new GameObject("UiHost");
			var document = mHost.AddComponent<UIDocument>();
			document.panelSettings = panelSettings;
			document.visualTreeAsset = visualTree;

			// Siblings, so neither becomes the other's parent scope; both resolve against the document.
			var first = AddSiblingView(mHost, firstName);
			var second = AddSiblingView(mHost, secondName);

			return (first, second);
		}

		private static CounterView AddSiblingView(GameObject documentHost, string rootName)
		{
			var host = new GameObject($"{rootName}View");
			host.transform.SetParent(documentHost.transform);
			var view = host.AddComponent<CounterView>();
			SetRootName(view, rootName);
			return view;
		}

		// A view whose root is a leaf element cannot use CounterView: that one queries a "Reset"
		// descendant, and Q() does not match the element it is called on.
		private static BareView CreateChildView(ViewRoot parent, string rootName)
		{
			var childHost = new GameObject("NestedView");
			childHost.transform.SetParent(parent.transform);
			var child = childHost.AddComponent<BareView>();
			SetRootName(child, rootName);
			return child;
		}

		private static void SetRootName(ViewRoot view, string rootName)
		{
			var field = typeof(ViewRoot)
				.GetField("m_RootName", BindingFlags.Instance | BindingFlags.NonPublic);

			Assert.That(field, Is.Not.Null, "ViewRoot no longer has an m_RootName field");
			field!.SetValue(view, rootName);
		}

		[Test]
		public void Bind_ScopesRootLookup_SoTwoInstancesOfOneTemplateBindIndependently()
		{
			var (first, second) = CreateSiblingViews("FirstCounter", "SecondCounter");
			var firstViewModel = new CounterViewModel();
			var secondViewModel = new CounterViewModel();

			first.Bind(firstViewModel);
			second.Bind(secondViewModel);

			Assert.That(first.Root, Is.Not.SameAs(second.Root));
			Assert.That(first.Root.dataSource, Is.SameAs(firstViewModel));
			Assert.That(second.Root.dataSource, Is.SameAs(secondViewModel));
		}

		[Test]
		public void Bind_ResolvesNestedViewInsideItsParentScope_NotASibling()
		{
			var (first, second) = CreateSiblingViews("FirstCounter", "SecondCounter");
			first.Bind(new CounterViewModel());
			second.Bind(new CounterViewModel());

			var nested = CreateChildView(first, "Reset");
			nested.Bind(new CounterViewModel());

			Assert.That(nested.Root, Is.SameAs(first.Root.Q("Reset")));
			Assert.That(nested.Root, Is.Not.SameAs(second.Root.Q("Reset")));
		}

		[Test]
		public void Bind_ResolvesNestedView_EvenWhenTheParentHasNotBoundYet()
		{
			var (first, _) = CreateSiblingViews("FirstCounter", "SecondCounter");
			var nested = CreateChildView(first, "Reset");

			nested.Bind(new CounterViewModel());

			Assert.That(nested.Root, Is.SameAs(first.ResolveRoot().Q("Reset")));
		}

		[Test]
		public void Bind_ThrowsForANestedView_WhenTheNameExistsOnlyOutsideTheParentScope()
		{
			var (first, _) = CreateSiblingViews("FirstCounter", "SecondCounter");
			var nested = CreateChildView(first, "SecondCounter");

			var exception = Assert.Throws<MissingComponentException>(
				() => nested.Bind(new CounterViewModel()));

			Assert.That(exception!.Message, Does.Contain("SecondCounter"));
		}

		private class BareView : View<CounterViewModel>
		{
			public VisualElement Root => mRoot;
		}

		private class CounterView : View<CounterViewModel>
		{
			public VisualElement Root => mRoot;
			public Button? ResetButton { get; private set; }

			public override void Bind(CounterViewModel viewModel)
			{
				base.Bind(viewModel);

				ResetButton = mRoot.Q<Button>("Reset");
				ResetButton.clicked += mViewModel.OnResetClicked;
			}
		}

		private class CounterViewModel
		{
			[CreateProperty] public string Counter { get; private set; } = "0";

			public int ResetClickCount { get; private set; }

			public void OnResetClicked() => ResetClickCount++;
		}
	}
}
