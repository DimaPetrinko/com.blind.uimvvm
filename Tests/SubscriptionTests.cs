#nullable enable
using System;
using System.Collections.Generic;
using Blind.UiMvvm.Disposables;
using Blind.UiMvvm.ReactiveProperties;
using Blind.UiMvvm.ReactiveProperties.Implementation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blind.UiMvvm.Tests
{
	public class SubscriptionTests
	{
		[Test]
		public void Subscribe_CallsTheHandlerWithTheCurrentValue()
		{
			var property = new ReactiveProperty<int>(7);
			var seen = new List<int>();

			property.Subscribe(seen.Add);

			Assert.That(seen, Is.EqualTo(new[] { 7 }));
		}

		[Test]
		public void Subscribe_WithoutPriming_WaitsForAChange()
		{
			var property = new ReactiveProperty<int>(7);
			var seen = new List<int>();

			property.Subscribe(seen.Add, false);

			Assert.That(seen, Is.Empty);

			property.Value = 8;

			Assert.That(seen, Is.EqualTo(new[] { 8 }));
		}

		[Test]
		public void DisposingTheSubscription_StopsNotifications()
		{
			var property = new ReactiveProperty<int>();
			var seen = new List<int>();
			var subscription = property.Subscribe(seen.Add, false);

			property.Value = 1;
			subscription.Dispose();
			property.Value = 2;

			Assert.That(seen, Is.EqualTo(new[] { 1 }));
		}

		[Test]
		public void DisposingTwice_IsSafe()
		{
			var property = new ReactiveProperty<int>();
			var subscription = property.Subscribe(_ => { }, false);

			subscription.Dispose();

			Assert.DoesNotThrow(() => subscription.Dispose());
		}

		/// <summary>
		/// The caller never receives the handle it would need to clean up, so a subscription left
		/// registered here could never be removed.
		/// </summary>
		[Test]
		public void Subscribe_WhenPrimingThrows_LeavesNothingSubscribed()
		{
			var property = new ReactiveProperty<int>(1);
			var calls = 0;

			Assert.Throws<InvalidOperationException>(() => property.Subscribe(_ =>
			{
				calls++;
				throw new InvalidOperationException("boom");
			}));

			property.Value = 2;

			Assert.That(calls, Is.EqualTo(1), "the handler was still subscribed after the throw");
		}

		[Test]
		public void CompositeDisposable_DisposesEverythingItHolds_InReverseOrder()
		{
			var order = new List<int>();
			var composite = new CompositeDisposable();

			composite.Add(new Spy(() => order.Add(1)));
			composite.Add(new Spy(() => order.Add(2)));

			composite.Dispose();

			Assert.That(order, Is.EqualTo(new[] { 2, 1 }));
		}

		[Test]
		public void CompositeDisposable_DisposesOnlyOnce()
		{
			var disposals = 0;
			var composite = new CompositeDisposable();
			composite.Add(new Spy(() => disposals++));

			composite.Dispose();
			composite.Dispose();

			Assert.That(disposals, Is.EqualTo(1));
			Assert.That(composite.IsDisposed, Is.True);
		}

		/// <summary>
		/// A late Add from an async continuation must not resurrect a screen that is already gone.
		/// </summary>
		[Test]
		public void CompositeDisposable_DisposesAnythingAddedAfterItWasDisposed()
		{
			var disposed = false;
			var composite = new CompositeDisposable();
			composite.Dispose();

			composite.Add(new Spy(() => disposed = true));

			Assert.That(disposed, Is.True);
			Assert.That(composite.Count, Is.Zero);
		}

		[Test]
		public void CompositeDisposable_OneThrowingDisposable_DoesNotStrandTheRest()
		{
			var disposed = false;
			var composite = new CompositeDisposable();

			composite.Add(new Spy(() => disposed = true));
			composite.Add(new Spy(() => throw new InvalidOperationException("boom")));

			LogAssert.Expect(LogType.Exception, "InvalidOperationException: boom");

			composite.Dispose();

			Assert.That(disposed, Is.True);
		}

		[Test]
		public void CompositeDisposable_Clear_DisposesAndStaysUsable()
		{
			var disposals = 0;
			var composite = new CompositeDisposable();
			composite.Add(new Spy(() => disposals++));

			composite.Clear();
			composite.Add(new Spy(() => disposals++));
			composite.Dispose();

			Assert.That(disposals, Is.EqualTo(2));
		}

		[Test]
		public void ForceNotify_RaisesWithoutAChange()
		{
			var property = new ReactiveProperty<string>("same");
			var calls = 0;
			property.Subscribe(_ => calls++, false);

			property.Value = "same";
			Assert.That(calls, Is.Zero);

			property.ForceNotify();

			Assert.That(calls, Is.EqualTo(1));
		}

		private class Spy : IDisposable
		{
			private readonly Action mOnDispose;

			public Spy(Action onDispose) => mOnDispose = onDispose;

			public void Dispose() => mOnDispose();
		}
	}
}
