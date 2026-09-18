#nullable enable
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Blind.UiMvvm.Binding;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blind.UiMvvm.Tests
{
	public class ViewBindingTests
	{
		[Test]
		public void TryBind_BindsAndReportsSuccess_WhenTheReferenceIsAssigned()
		{
			var view = new FakeBindable();
			var viewModel = new FakeViewModel();

			var bound = ViewBinding.TryBind(view, viewModel);

			Assert.That(bound, Is.True);
			Assert.That(view.BoundTo, Is.SameAs(viewModel));
		}

		[Test]
		public void TryBind_AcceptsAnyBindable_NotOnlyAView()
		{
			var host = new GameObject("BindableComponent");
			try
			{
				var component = host.AddComponent<FakeBindableComponent>();

				var bound = ViewBinding.TryBind(component, new FakeViewModel());

				Assert.That(bound, Is.True);
				Assert.That(component.BindCount, Is.EqualTo(1));
			}
			finally
			{
				Object.DestroyImmediate(host);
			}
		}

		[Test]
		public void TryBind_ReportsFailureAndNamesTheViewModel_WhenTheReferenceIsNull()
		{
			LogAssert.Expect(LogType.Error, new Regex(nameof(FakeViewModel)));

			var bound = ViewBinding.TryBind<FakeBindable, FakeViewModel>(null, new FakeViewModel());

			Assert.That(bound, Is.False);
		}

		[Test]
		public void TryBind_RoutesTheMessageToTheSuppliedLogger_AndUsesTheSuppliedName()
		{
			var messages = new List<string>();

			var bound = ViewBinding.TryBind<FakeBindable, FakeViewModel>(
				null, new FakeViewModel(), messages.Add, "Shop");

			Assert.That(bound, Is.False);
			Assert.That(messages, Has.Count.EqualTo(1));
			Assert.That(messages[0], Does.Contain("Shop"));
		}

		/// <summary>
		/// The reason the null check is written out rather than left as <c>view != null</c>: a destroyed
		/// component is not null by reference, and on a type parameter the operator does not reach
		/// Unity's overload. Binding it would throw <c>MissingReferenceException</c> instead of saying
		/// which reference was gone.
		/// </summary>
		[Test]
		public void TryBind_ReportsFailure_WhenTheComponentHasBeenDestroyed()
		{
			var host = new GameObject("DestroyedComponent");
			var component = host.AddComponent<FakeBindableComponent>();
			Object.DestroyImmediate(host);

			var messages = new List<string>();

			var bound = ViewBinding.TryBind(component, new FakeViewModel(), messages.Add);

			Assert.That(bound, Is.False);
			Assert.That(messages, Has.Count.EqualTo(1));
		}

		private class FakeViewModel
		{
		}

		private class FakeBindable : IBindable<FakeViewModel>
		{
			public FakeViewModel? BoundTo { get; private set; }

			public void Bind(FakeViewModel vm) => BoundTo = vm;
		}

		private class FakeBindableComponent : MonoBehaviour, IBindable<FakeViewModel>
		{
			public int BindCount { get; private set; }

			public void Bind(FakeViewModel vm) => BindCount++;
		}
	}
}
