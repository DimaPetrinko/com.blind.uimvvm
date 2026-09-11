#nullable enable
using System;
using Blind.UiMvvm.ReactiveProperties;
using Blind.UiMvvm.ReactiveProperties.Implementation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace Blind.UiMvvm.Tests
{
	public class ReactivePropertyTests
	{
		[Test]
		public void Constructor_WithDefaultValue_InitializesValueToDefault()
		{
			var property = new ReactiveProperty<int>();

			Assert.That(property.Value, Is.EqualTo(0));
		}

		[Test]
		public void Constructor_WithInitialValue_InitializesValueCorrectly()
		{
			var property = new ReactiveProperty<int>(42);

			Assert.That(property.Value, Is.EqualTo(42));
		}

		[Test]
		public void Constructor_WithReferenceType_InitializesValueCorrectly()
		{
			var testString = "test";
			var property = new ReactiveProperty<string>(testString);

			Assert.That(property.Value, Is.EqualTo(testString));
		}

		[Test]
		public void Value_WhenSet_ChangesValue()
		{
			var property = new ReactiveProperty<int>();

			property.Value = 42;

			Assert.That(property.Value, Is.EqualTo(42));
		}

		[Test]
		public void Value_WhenChanged_FiresChangedEvent()
		{
			var property = new ReactiveProperty<int>();
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = 42;

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void Value_WhenChanged_PassesNewValueToEvent()
		{
			var property = new ReactiveProperty<int>();
			var receivedValue = 0;
			property.Changed += value => receivedValue = value;

			property.Value = 42;

			Assert.That(receivedValue, Is.EqualTo(42));
		}

		[Test]
		public void Value_WhenSetToSameValue_DoesNotFireEvent()
		{
			var property = new ReactiveProperty<int>(42);
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = 42;

			Assert.That(eventFired, Is.False);
		}

		[Test]
		public void Value_WhenSetMultipleTimes_FiresEventEachTime()
		{
			var property = new ReactiveProperty<int>();
			var eventCount = 0;
			property.Changed += _ => eventCount++;

			property.Value = 1;
			property.Value = 2;
			property.Value = 3;

			Assert.That(eventCount, Is.EqualTo(3));
		}

		[Test]
		public void Value_WhenSetBackAndForth_FiresEventEachTime()
		{
			var property = new ReactiveProperty<int>();
			var eventCount = 0;
			property.Changed += _ => eventCount++;

			property.Value = 1;
			property.Value = 0;
			property.Value = 1;

			Assert.That(eventCount, Is.EqualTo(3));
		}

		[Test]
		public void Changed_WithMultipleSubscribers_NotifiesAllSubscribers()
		{
			var property = new ReactiveProperty<int>();
			var subscriber1Notified = false;
			var subscriber2Notified = false;
			var subscriber3Notified = false;

			property.Changed += _ => subscriber1Notified = true;
			property.Changed += _ => subscriber2Notified = true;
			property.Changed += _ => subscriber3Notified = true;

			property.Value = 42;

			Assert.That(subscriber1Notified, Is.True);
			Assert.That(subscriber2Notified, Is.True);
			Assert.That(subscriber3Notified, Is.True);
		}

		[Test]
		public void Changed_WhenUnsubscribed_DoesNotNotifyUnsubscribedHandler()
		{
			var property = new ReactiveProperty<int>();
			var notifiedCount = 0;
			Action<int> handler = _ => notifiedCount++;

			property.Changed += handler;
			property.Value = 1;

			property.Changed -= handler;
			property.Value = 2;

			Assert.That(notifiedCount, Is.EqualTo(1));
		}

		[Test]
		public void Value_WithFloat_DetectsChangesCorrectly()
		{
			var property = new ReactiveProperty<float>(1.0f);
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = 1.1f;

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void Value_WithFloat_DoesNotFireForSameValue()
		{
			var property = new ReactiveProperty<float>(1.0f);
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = 1.0f;

			Assert.That(eventFired, Is.False);
		}

		[Test]
		public void Value_WithString_DetectsChangesCorrectly()
		{
			var property = new ReactiveProperty<string>("initial");
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = "changed";

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void Value_WithString_DoesNotFireForSameValue()
		{
			var property = new ReactiveProperty<string>("test");
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = "test";

			Assert.That(eventFired, Is.False);
		}

		[Test]
		public void Value_WithNullString_HandlesNullCorrectly()
		{
			var property = new ReactiveProperty<string>();

			Assert.That(property.Value, Is.Null);
		}

		[Test]
		public void Value_ChangingFromNullToValue_FiresEvent()
		{
			var property = new ReactiveProperty<string>();
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = "test";

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void Value_ChangingFromValueToNull_FiresEvent()
		{
			var property = new ReactiveProperty<string?>("test");
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = null;

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void Value_WithBool_DetectsChangesCorrectly()
		{
			var property = new ReactiveProperty<bool>();
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = true;

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void ImplicitConversion_ConvertsToValue()
		{
			var property = new ReactiveProperty<int>(42);

			int value = property;

			Assert.That(value, Is.EqualTo(42));
		}

		[Test]
		public void ImplicitConversion_WorksInArithmetic()
		{
			var property = new ReactiveProperty<int>(10);

			var result = property + 5;

			Assert.That(result, Is.EqualTo(15));
		}

		[Test]
		public void ImplicitConversion_WorksInComparison()
		{
			var property = new ReactiveProperty<int>(42);

			var isGreater = property > 40;

			Assert.That(isGreater, Is.True);
		}

		[Test]
		public void Value_WithLong_HandlesLargeValues()
		{
			var property = new ReactiveProperty<long>(31536000000L);

			Assert.That(property.Value, Is.EqualTo(31536000000L));
		}

		[Test]
		public void Value_WithLong_DetectsChanges()
		{
			var property = new ReactiveProperty<long>(100L);
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = 200L;

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void ReactiveProperty_ImplementsIReactiveProperty()
		{
			var property = new ReactiveProperty<int>();

			Assert.That(property, Is.InstanceOf<IReactiveProperty<int>>());
		}

		[Test]
		public void ReactiveProperty_ImplementsIReadOnlyReactiveProperty()
		{
			var property = new ReactiveProperty<int>();

			Assert.That(property, Is.InstanceOf<IReadOnlyReactiveProperty<int>>());
		}

		[Test]
		public void IReactiveProperty_CanBeAssignedToIReadOnlyReactiveProperty()
		{
			IReactiveProperty<int> writableProperty = new ReactiveProperty<int>(42);

			IReadOnlyReactiveProperty<int> readOnlyProperty = writableProperty;

			Assert.That(readOnlyProperty.Value, Is.EqualTo(42));
		}

		[Test]
		public void IReadOnlyReactiveProperty_CanSubscribeToChanges()
		{
			IReadOnlyReactiveProperty<int> property = new ReactiveProperty<int>();
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			((IReactiveProperty<int>)property).Value = 42;

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void Changed_EventHandlerException_DoesNotPreventOtherHandlers()
		{
			var property = new ReactiveProperty<int>();
			var handler1Called = false;
			var handler2Called = false;

			property.Changed += _ => { throw new Exception("Test exception"); };
			property.Changed += _ => handler1Called = true;
			property.Changed += _ => handler2Called = true;

			LogAssert.Expect(LogType.Exception, "Exception: Test exception");
			property.Value = 42;

			Assert.That(handler1Called, Is.True);
			Assert.That(handler2Called, Is.True);
		}

		[Test]
		public void Value_ThreadSafety_MultipleReadsWork()
		{
			var property = new ReactiveProperty<int>(42);

			var value1 = property.Value;
			var value2 = property.Value;
			var value3 = property.Value;

			Assert.That(value1, Is.EqualTo(42));
			Assert.That(value2, Is.EqualTo(42));
			Assert.That(value3, Is.EqualTo(42));
		}

		[Test]
		public void Changed_WithStruct_FiresEventCorrectly()
		{
			var property = new ReactiveProperty<Vector2>(new Vector2(1, 2));
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = new Vector2(3, 4);

			Assert.That(eventFired, Is.True);
		}

		[Test]
		public void Changed_WithStruct_DoesNotFireForEqualValue()
		{
			var property = new ReactiveProperty<Vector2>(new Vector2(1, 2));
			var eventFired = false;
			property.Changed += _ => eventFired = true;

			property.Value = new Vector2(1, 2);

			Assert.That(eventFired, Is.False);
		}
	}
}