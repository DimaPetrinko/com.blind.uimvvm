#nullable enable
using System;
using System.Collections.Generic;
using Blind.Reactive.Implementation;
using NUnit.Framework;

namespace Blind.Reactive.Tests
{
	public class ReactivePropertyIntegrationTests
	{
		[Test]
		public void IntegrationTest_MultiplePropertiesInDataClass_WorkIndependently()
		{
			var data = new TestData();
			var counterChanged = false;
			var nameChanged = false;
			var isActiveChanged = false;

			data.Counter.Changed += _ => counterChanged = true;
			data.Name.Changed += _ => nameChanged = true;
			data.IsActive.Changed += _ => isActiveChanged = true;

			data.Counter.Value = 10;

			Assert.That(counterChanged, Is.True);
			Assert.That(nameChanged, Is.False);
			Assert.That(isActiveChanged, Is.False);
		}

		[Test]
		public void IntegrationTest_ReadOnlyConsumer_CanReadAndObserve()
		{
			var property = new ReactiveProperty<int>();
			var consumer = new ReadOnlyConsumer(property);

			property.Value = 42;

			Assert.That(consumer.GetCurrentValue(), Is.EqualTo(42));
			Assert.That(consumer.LastValue, Is.EqualTo(42));
			Assert.That(consumer.NotificationCount, Is.EqualTo(1));
		}

		[Test]
		public void IntegrationTest_WriteableConsumer_CanModifyProperty()
		{
			var property = new ReactiveProperty<int>();
			var consumer = new WriteableConsumer(property);

			consumer.Increment();

			Assert.That(property.Value, Is.EqualTo(1));
		}

		[Test]
		public void IntegrationTest_MultipleConsumers_AllGetNotified()
		{
			var property = new ReactiveProperty<int>();
			var consumer1 = new ReadOnlyConsumer(property);
			var consumer2 = new ReadOnlyConsumer(property);
			var consumer3 = new WriteableConsumer(property);

			consumer3.SetValue(100);

			Assert.That(consumer1.LastValue, Is.EqualTo(100));
			Assert.That(consumer2.LastValue, Is.EqualTo(100));
			Assert.That(property.Value, Is.EqualTo(100));
		}

		[Test]
		public void IntegrationTest_WriterAndReader_CommunicateThroughProperty()
		{
			var property = new ReactiveProperty<int>();
			var reader = new ReadOnlyConsumer(property);
			var writer = new WriteableConsumer(property);

			writer.Increment();
			writer.Increment();
			writer.Increment();

			Assert.That(reader.GetCurrentValue(), Is.EqualTo(3));
			Assert.That(reader.NotificationCount, Is.EqualTo(3));
		}

		[Test]
		public void IntegrationTest_IReactivePropertyToIReadOnly_CanBePassedAsReadOnly()
		{
			IReactiveProperty<int> writableProperty = new ReactiveProperty<int>(42);
			IReadOnlyReactiveProperty<int> readOnlyReference = writableProperty;

			Assert.That(readOnlyReference.Value, Is.EqualTo(42));

			writableProperty.Value = 100;

			Assert.That(readOnlyReference.Value, Is.EqualTo(100));
		}

		[Test]
		public void IntegrationTest_CascadingUpdates_PropagateThroughMultipleLayers()
		{
			var sourceProperty = new ReactiveProperty<int>();
			var derivedValue = 0;

			sourceProperty.Changed += value => derivedValue = value * 2;

			sourceProperty.Value = 10;

			Assert.That(derivedValue, Is.EqualTo(20));
		}

		[Test]
		public void IntegrationTest_MultiplePropertiesChained_UpdatesPropagate()
		{
			var property1 = new ReactiveProperty<int>();
			var property2 = new ReactiveProperty<int>();
			var property3 = new ReactiveProperty<int>();

			property1.Changed += value => property2.Value = value * 2;
			property2.Changed += value => property3.Value = value + 10;

			property1.Value = 5;

			Assert.That(property2.Value, Is.EqualTo(10));
			Assert.That(property3.Value, Is.EqualTo(20));
		}

		[Test]
		public void IntegrationTest_CollectionOfProperties_CanBeManaged()
		{
			var properties = new List<IReactiveProperty<int>>
			{
				new ReactiveProperty<int>(1),
				new ReactiveProperty<int>(2),
				new ReactiveProperty<int>(3)
			};

			var totalChanges = 0;
			foreach (var property in properties) property.Changed += _ => totalChanges++;

			properties[0].Value = 10;
			properties[1].Value = 20;
			properties[2].Value = 30;

			Assert.That(totalChanges, Is.EqualTo(3));
			Assert.That(properties[0].Value, Is.EqualTo(10));
			Assert.That(properties[1].Value, Is.EqualTo(20));
			Assert.That(properties[2].Value, Is.EqualTo(30));
		}

		[Test]
		public void IntegrationTest_PropertyInDataClass_MaintainsEncapsulation()
		{
			var data = new TestData();

			Assert.That(data.Counter, Is.InstanceOf<IReactiveProperty<int>>());
			Assert.That(data.Name, Is.InstanceOf<IReactiveProperty<string>>());
			Assert.That(data.IsActive, Is.InstanceOf<IReactiveProperty<bool>>());
		}

		[Test]
		public void IntegrationTest_ComputedValue_UpdatesOnSourceChange()
		{
			var firstName = new ReactiveProperty<string>("John");
			var lastName = new ReactiveProperty<string>("Doe");
			var fullName = "";

			Action updateFullName = () => fullName = $"{firstName.Value} {lastName.Value}";

			firstName.Changed += _ => updateFullName();
			lastName.Changed += _ => updateFullName();

			updateFullName();

			Assert.That(fullName, Is.EqualTo("John Doe"));

			firstName.Value = "Jane";

			Assert.That(fullName, Is.EqualTo("Jane Doe"));
		}

		[Test]
		public void IntegrationTest_CircularDependency_DoesNotCauseInfiniteLoop()
		{
			var property1 = new ReactiveProperty<int>();
			var property2 = new ReactiveProperty<int>();
			var updateCount = 0;

			property1.Changed += value =>
			{
				updateCount++;
				property2.Value = value;
			};

			property2.Changed += value =>
			{
				updateCount++;
				property1.Value = value;
			};

			property1.Value = 5;

			Assert.That(property1.Value, Is.EqualTo(5));
			Assert.That(property2.Value, Is.EqualTo(5));
			Assert.That(updateCount, Is.EqualTo(2));
		}

		[Test]
		public void IntegrationTest_DependentProperties_UpdateInCorrectOrder()
		{
			var baseValue = new ReactiveProperty<int>(10);
			var executionOrder = new List<string>();

			baseValue.Changed += value => { executionOrder.Add("first"); };
			baseValue.Changed += value => { executionOrder.Add("second"); };
			baseValue.Changed += value => { executionOrder.Add("third"); };

			baseValue.Value = 5;

			Assert.That(executionOrder.Count, Is.EqualTo(3));
			Assert.That(executionOrder[0], Is.EqualTo("first"));
			Assert.That(executionOrder[1], Is.EqualTo("second"));
			Assert.That(executionOrder[2], Is.EqualTo("third"));
		}

		[Test]
		public void IntegrationTest_UnsubscribeInHandler_WorksCorrectly()
		{
			var property = new ReactiveProperty<int>();
			var callCount = 0;
			Action<int>? handler = null;

			handler = value =>
			{
				callCount++;
				if (value >= 5)
					property.Changed -= handler!;
			};

			property.Changed += handler;

			property.Value = 1;
			property.Value = 5;
			property.Value = 10;

			Assert.That(callCount, Is.EqualTo(2));
		}

		[Test]
		public void IntegrationTest_DataClassWithReadOnlyExposure_PreventsExternalModification()
		{
			var data = new TestData();
			IReadOnlyReactiveProperty<int> readOnlyCounter = data.Counter;

			Assert.That(readOnlyCounter, Is.InstanceOf<IReadOnlyReactiveProperty<int>>());

			var canRead = readOnlyCounter.Value;
			Assert.That(canRead, Is.EqualTo(0));

			data.Counter.Value = 42;
			Assert.That(readOnlyCounter.Value, Is.EqualTo(42));
		}

		private class TestData
		{
			public TestData()
			{
				Counter = new ReactiveProperty<int>();
				Name = new ReactiveProperty<string>("Default");
				IsActive = new ReactiveProperty<bool>(true);
			}

			public IReactiveProperty<int> Counter { get; }
			public IReactiveProperty<string> Name { get; }
			public IReactiveProperty<bool> IsActive { get; }
		}

		private class ReadOnlyConsumer
		{
			private readonly IReadOnlyReactiveProperty<int> mCounter;

			public ReadOnlyConsumer(IReadOnlyReactiveProperty<int> counter)
			{
				mCounter = counter;
				mCounter.Changed += OnCounterChanged;
			}

			public int LastValue { get; private set; }
			public int NotificationCount { get; private set; }

			private void OnCounterChanged(int value)
			{
				LastValue = value;
				NotificationCount++;
			}

			public int GetCurrentValue()
			{
				return mCounter.Value;
			}
		}

		private class WriteableConsumer
		{
			private readonly IReactiveProperty<int> mCounter;

			public WriteableConsumer(IReactiveProperty<int> counter)
			{
				mCounter = counter;
			}

			public void Increment()
			{
				mCounter.Value++;
			}

			public void SetValue(int value)
			{
				mCounter.Value = value;
			}
		}
	}
}