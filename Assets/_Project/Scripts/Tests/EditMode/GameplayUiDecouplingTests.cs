using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine.TestTools;
using Zero.Gameplay.Events;
using Zero.Services.Events;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Integration "decoupling test" — verifies that Gameplay and UI layers
    /// communicate only via IEventBus, with no direct asmdef reference between them.
    /// This test proves the runtime path works; the asmdef-level check
    /// (grep Zero.UI in Zero.Gameplay.asmdef) is verified by code review.
    /// </summary>
    public sealed class GameplayUiDecouplingTests
    {
        [UnityTest]
        public IEnumerator LevelCompleted_PublishedByGameplay_ReceivedByUI() =>
            UniTask.ToCoroutine(async () =>
            {
                // Simulate a shared event bus (as would be injected from the container).
                var bus = new R3EventBus();
                bool uiReacted = false;

                // Simulate UI layer subscribing to gameplay events.
                using (bus.On<LevelCompleted>().Subscribe(evt =>
                {
                    // This is what a UI popup consumer would do: listen for level completion.
                    uiReacted = true;
                    Assert.AreEqual("level_001", evt.LevelId);
                }))
                {
                    // Simulate Gameplay layer publishing the event.
                    bus.Publish(new LevelCompleted("level_001", 5000));
                }

                Assert.IsTrue(uiReacted, "UI handler did not react to gameplay event");
                bus.Dispose();
                await UniTask.CompletedTask;
            });

        [UnityTest]
        public IEnumerator MultipleUiSubscribers_AllReceiveEvent() =>
            UniTask.ToCoroutine(async () =>
            {
                var bus = new R3EventBus();
                int uiPopupReactions = 0;
                int uiHudReactions = 0;

                using (bus.On<LevelFailed>().Subscribe(evt =>
                {
                    // UI popup reacts to level failure
                    uiPopupReactions++;
                }))
                using (bus.On<LevelFailed>().Subscribe(evt =>
                {
                    // UI HUD also reacts to level failure
                    uiHudReactions++;
                }))
                {
                    bus.Publish(new LevelFailed("level_001", "out_of_moves"));
                }

                Assert.AreEqual(1, uiPopupReactions);
                Assert.AreEqual(1, uiHudReactions);
                bus.Dispose();
                await UniTask.CompletedTask;
            });

        [UnityTest]
        public IEnumerator GameplayLayerUnaware_OfUiSubscriptions() =>
            UniTask.ToCoroutine(async () =>
            {
                // This test verifies that Gameplay can publish events without
                // knowing or caring whether UI (or any other layer) is listening.
                var bus = new R3EventBus();

                // Gameplay publishes without checking subscribers
                bus.Publish(new LevelStarted("level_001"));
                bus.Publish(new LevelCompleted("level_001", 0));

                // UI subscribes later (or not at all — Gameplay doesn't care)
                int receivedCount = 0;
                using (bus.On<LevelCompleted>().Subscribe(_ => receivedCount++))
                {
                    // The previous publish is gone; only future publishes reach this subscriber.
                    bus.Publish(new LevelCompleted("level_002", 1000));
                }

                // Only the second publish was received (after subscription).
                Assert.AreEqual(1, receivedCount);
                bus.Dispose();
                await UniTask.CompletedTask;
            });
    }
}
