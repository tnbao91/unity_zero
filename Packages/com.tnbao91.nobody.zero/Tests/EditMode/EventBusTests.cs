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
        private R3EventBus _bus;

        [SetUp]
        public void SetUp() => _bus = new R3EventBus();

        [TearDown]
        public void TearDown() => _bus?.Dispose();

        [Test]
        public void PublishDeliversToSubscriber()
        {
            int received = 0;
            using var sub = _bus.On<int>().Subscribe(v => received = v);
            _bus.Publish(42);
            Assert.AreEqual(42, received);
        }

        [Test]
        public void MultipleSubscribersAllReceive()
        {
            int a = 0, b = 0, c = 0;
            using var s1 = _bus.On<int>().Subscribe(v => a = v);
            using var s2 = _bus.On<int>().Subscribe(v => b = v);
            using var s3 = _bus.On<int>().Subscribe(v => c = v);
            _bus.Publish(99);
            Assert.AreEqual(99, a);
            Assert.AreEqual(99, b);
            Assert.AreEqual(99, c);
        }

        [Test]
        public void TypeIsolatedStreams()
        {
            bool intHit = false;
            bool stringHit = false;
            using var sInt = _bus.On<int>().Subscribe(_ => intHit = true);
            using var sStr = _bus.On<string>().Subscribe(_ => stringHit = true);

            _bus.Publish("hi");
            Assert.IsFalse(intHit, "int subscriber must not see string events.");
            Assert.IsTrue(stringHit);
        }

        [Test]
        public void DisposingSubscriptionStopsDelivery()
        {
            int count = 0;
            var sub = _bus.On<int>().Subscribe(_ => count++);
            _bus.Publish(1);
            Assert.AreEqual(1, count);

            sub.Dispose();
            _bus.Publish(2);
            Assert.AreEqual(1, count, "Disposed subscription must not receive further events.");
        }

        [Test]
        public void ValueTypeEventDelivered()
        {
            var seen = new List<int>();
            using var sub = _bus.On<TickEvent>().Subscribe(e => seen.Add(e.Frame));
            _bus.Publish(new TickEvent { Frame = 1 });
            _bus.Publish(new TickEvent { Frame = 2 });
            Assert.AreEqual(new[] { 1, 2 }, seen);
        }

        [Test]
        public void LateSubscriberDoesNotReplay()
        {
            _bus.Publish(7);
            int received = -1;
            using var sub = _bus.On<int>().Subscribe(v => received = v);
            Assert.AreEqual(-1, received, "R3 Subject is not behavior-replaying; late subscribers see no history.");
        }

        private struct TickEvent
        {
            public int Frame;
        }
    }
}
