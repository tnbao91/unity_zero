using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Gameplay.Events;
using Zero.Services.Events;

namespace Zero.Tests.EditMode
{
    public sealed class LevelLifecycleEventsTests
    {
        private sealed class StubLogService : ILogService
        {
            public bool IsEnabled => true;
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warning(string message) { }
            public void Error(string message) { }
        }

        [UnityTest]
        public IEnumerator LevelStarted_PublishedAndSubscribed() =>
            UniTask.ToCoroutine(async () =>
            {
                var bus = new R3EventBus();
                var received = new System.Collections.Generic.List<LevelStarted>();

                using (bus.On<LevelStarted>().Subscribe(evt => received.Add(evt)))
                {
                    var evt = new LevelStarted("level_001");
                    bus.Publish(evt);

                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual("level_001", received[0].LevelId);
                }

                bus.Dispose();
                await UniTask.CompletedTask;
            });

        [UnityTest]
        public IEnumerator LevelCompleted_PublishedAndSubscribed() =>
            UniTask.ToCoroutine(async () =>
            {
                var bus = new R3EventBus();
                var received = new System.Collections.Generic.List<LevelCompleted>();

                using (bus.On<LevelCompleted>().Subscribe(evt => received.Add(evt)))
                {
                    var evt = new LevelCompleted("level_001", 1000);
                    bus.Publish(evt);

                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual("level_001", received[0].LevelId);
                    Assert.AreEqual(1000, received[0].Score);
                }

                bus.Dispose();
                await UniTask.CompletedTask;
            });

        [UnityTest]
        public IEnumerator LevelFailed_PublishedAndSubscribed() =>
            UniTask.ToCoroutine(async () =>
            {
                var bus = new R3EventBus();
                var received = new System.Collections.Generic.List<LevelFailed>();

                using (bus.On<LevelFailed>().Subscribe(evt => received.Add(evt)))
                {
                    var evt = new LevelFailed("level_001", "timeout");
                    bus.Publish(evt);

                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual("level_001", received[0].LevelId);
                    Assert.AreEqual("timeout", received[0].Reason);
                }

                bus.Dispose();
                await UniTask.CompletedTask;
            });

        [UnityTest]
        public IEnumerator LevelRestarted_PublishedAndSubscribed() =>
            UniTask.ToCoroutine(async () =>
            {
                var bus = new R3EventBus();
                var received = new System.Collections.Generic.List<LevelRestarted>();

                using (bus.On<LevelRestarted>().Subscribe(evt => received.Add(evt)))
                {
                    var evt = new LevelRestarted("level_001");
                    bus.Publish(evt);

                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual("level_001", received[0].LevelId);
                }

                bus.Dispose();
                await UniTask.CompletedTask;
            });

        [UnityTest]
        public IEnumerator LevelExited_PublishedAndSubscribed() =>
            UniTask.ToCoroutine(async () =>
            {
                var bus = new R3EventBus();
                var received = new System.Collections.Generic.List<LevelExited>();

                using (bus.On<LevelExited>().Subscribe(evt => received.Add(evt)))
                {
                    var evt = new LevelExited("level_001");
                    bus.Publish(evt);

                    Assert.AreEqual(1, received.Count);
                    Assert.AreEqual("level_001", received[0].LevelId);
                }

                bus.Dispose();
                await UniTask.CompletedTask;
            });
    }
}
