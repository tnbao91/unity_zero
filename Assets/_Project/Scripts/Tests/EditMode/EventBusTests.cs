using System;
using System.Collections.Generic;
using NUnit.Framework;
using R3;
using Zero.Core;
using Zero.Services.Events;

namespace Zero.Tests.EditMode
{
    [TestFixture]
    public sealed class EventBusTests
    {
        private IEventBus _bus;

        [SetUp]
        public void SetUp()
        {
            _bus = new R3EventBus();
        }

        [Test]
        public void PublishSubscribe()
        {
            int received = 0;
            using var sub = _bus.On<int>().Subscribe(value =>
            {
                received = value;
            });

            _bus.Publish(42);

            Assert.AreEqual(42, received);
        }

        [Test]
        public void MultipleSubscribers()
        {
            var received1 = 0;
            var received2 = 0;
            var received3 = 0;

            using var sub1 = _bus.On<int>().Subscribe(v => received1 = v);
            using var sub2 = _bus.On<int>().Subscribe(v => received2 = v);
            using var sub3 = _bus.On<int>().Subscribe(v => received3 = v);

            _bus.Publish(99);

            Assert.AreEqual(99, received1);
            Assert.AreEqual(99, received2);
            Assert.AreEqual(99, received3);
        }

        [Test]
        public void TypeIsolation()
        {
            var intReceived = false;
            var stringReceived = false;

            using var intSub = _bus.On<int>().Subscribe(_ => intReceived = true);
            using var stringSub = _bus.On<string>().Subscribe(_ => stringReceived = true);

            _bus.Publish("hello");

            Assert.IsFalse(intReceived, "Int subscriber should NOT receive string event.");
            Assert.IsTrue(stringReceived, "String subscriber should receive string event.");
        }

        [Test]
        public void Dispose()
        {
            var callCount = 0;

            var subscription = _bus.On<int>().Subscribe(_ => callCount++);

            _bus.Publish(1);
            Assert.AreEqual(1, callCount);

            subscription.Dispose();

            _bus.Publish(2);
            Assert.AreEqual(1, callCount, "Disposed subscription should not receive further events.");
        }

        [Test]
        public void RecordEventType()
        {
            var eventRecords = new List<object>();

            using var sub = _bus.On<EventRecord>().Subscribe(e => eventRecords.Add(e));

            _bus.Publish(new EventRecord { Value = 1 });
            _bus.Publish(new EventRecord { Value = 2 });

            Assert.AreEqual(2, eventRecords.Count);
            Assert.AreEqual(1, ((EventRecord)eventRecords[0]).Value);
            Assert.AreEqual(2, ((EventRecord)eventRecords[1]).Value);
        }

        [Test]
        public void StructEventType()
        {
            var values = new List<int>();

            using var sub = _bus.On<EventStruct>().Subscribe(e => values.Add(e.Value));

            _bus.Publish(new EventStruct { Value = 10 });
            _bus.Publish(new EventStruct { Value = 20 });

            Assert.AreEqual(2, values.Count);
            Assert.AreEqual(10, values[0]);
            Assert.AreEqual(20, values[1]);
        }

        /// <summary>
        /// Test event as a record type.
        /// </summary>
        private sealed class EventRecord
        {
            public int Value { get; set; }
        }

        /// <summary>
        /// Test event as a struct type.
        /// </summary>
        private struct EventStruct
        {
            public int Value { get; set; }
        }
    }
}
