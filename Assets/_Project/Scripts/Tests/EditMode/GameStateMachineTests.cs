using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using Zero.Gameplay;

namespace Zero.Tests.EditMode
{
    public sealed class GameStateMachineTests
    {
        private sealed class TestState : IGameState
        {
            public bool EnterCalled { get; private set; }
            public bool ExitCalled { get; private set; }

            public UniTask EnterAsync(CancellationToken ct)
            {
                EnterCalled = true;
                return UniTask.CompletedTask;
            }

            public UniTask ExitAsync(CancellationToken ct)
            {
                ExitCalled = true;
                return UniTask.CompletedTask;
            }

            public void Tick(float deltaTime)
            {
            }
        }

        [Test]
        public void CurrentState_InitiallyNull()
        {
            var machine = new GameStateMachine();
            Assert.IsNull(machine.CurrentState);
            machine.Dispose();
        }

        [UnityTest]
        public IEnumerator ChangeStateAsync_FirstTransition_SetsCurrentState() =>
            UniTask.ToCoroutine(async () =>
            {
                var machine = new GameStateMachine();
                var state1 = new TestState();

                await machine.ChangeStateAsync(state1);

                Assert.AreEqual(state1, machine.CurrentState);
                Assert.IsTrue(state1.EnterCalled);
                machine.Dispose();
            });

        [UnityTest]
        public IEnumerator ChangeStateAsync_Transition_CallsExitBeforeEnter() =>
            UniTask.ToCoroutine(async () =>
            {
                var machine = new GameStateMachine();
                var state1 = new TestState();
                var state2 = new TestState();

                await machine.ChangeStateAsync(state1);
                state1.ExitCalled = false;
                state2.EnterCalled = false;

                await machine.ChangeStateAsync(state2);

                Assert.IsTrue(state1.ExitCalled);
                Assert.IsTrue(state2.EnterCalled);
                Assert.AreEqual(state2, machine.CurrentState);
                machine.Dispose();
            });

        [UnityTest]
        public IEnumerator ChangeStateAsync_ReentryOfSameInstance_Throws() =>
            UniTask.ToCoroutine(async () =>
            {
                var machine = new GameStateMachine();
                var state1 = new TestState();

                await machine.ChangeStateAsync(state1);

                var ex = Assert.ThrowsAsync<InvalidOperationException>(
                    () => machine.ChangeStateAsync(state1).AsTask());
                Assert.That(ex.Message, Does.Contain("re-enter"));
                machine.Dispose();
            });

        [UnityTest]
        public IEnumerator ChangeStateAsync_NullState_Throws() =>
            UniTask.ToCoroutine(async () =>
            {
                var machine = new GameStateMachine();
                await Assert.ThrowsAsync<ArgumentNullException>(
                    () => machine.ChangeStateAsync(null).AsTask());
                machine.Dispose();
            });

        [UnityTest]
        public IEnumerator OnStateChanged_FiresOncePerTransition() =>
            UniTask.ToCoroutine(async () =>
            {
                var machine = new GameStateMachine();
                var state1 = new TestState();
                var state2 = new TestState();
                int fireCount = 0;

                using (machine.OnStateChanged.Subscribe(_ => fireCount++))
                {
                    await machine.ChangeStateAsync(state1);
                    Assert.AreEqual(1, fireCount);

                    await machine.ChangeStateAsync(state2);
                    Assert.AreEqual(2, fireCount);
                }

                machine.Dispose();
            });

        [UnityTest]
        public IEnumerator ChangeStateAsync_CancellationPropagates() =>
            UniTask.ToCoroutine(async () =>
            {
                var machine = new GameStateMachine();
                var state1 = new TestState();
                var cts = new CancellationTokenSource();
                cts.Cancel();

                var ex = Assert.ThrowsAsync<OperationCanceledException>(
                    () => machine.ChangeStateAsync(state1, cts.Token).AsTask());
                Assert.IsNotNull(ex);
                machine.Dispose();
            });

        [UnityTest]
        public IEnumerator ChangeStateAsync_ConcurrentCall_Throws() =>
            UniTask.ToCoroutine(async () =>
            {
                var machine = new GameStateMachine();
                var state1 = new SlowEnterState();
                var state2 = new TestState();

                // Start state1 (its EnterAsync awaits a delay, so the transition is mid-flight).
                var pending = machine.ChangeStateAsync(state1);

                // Second call while first is in progress must throw — consumer is responsible for sequencing.
                Assert.ThrowsAsync<InvalidOperationException>(
                    () => machine.ChangeStateAsync(state2).AsTask());

                state1.ReleaseEnter();
                await pending;
                Assert.AreEqual(state1, machine.CurrentState);
                machine.Dispose();
            });

        private sealed class SlowEnterState : IGameState
        {
            private readonly UniTaskCompletionSource _gate = new();
            public void ReleaseEnter() => _gate.TrySetResult();
            public async UniTask EnterAsync(CancellationToken ct) => await _gate.Task.AttachExternalCancellation(ct);
            public UniTask ExitAsync(CancellationToken ct) => UniTask.CompletedTask;
            public void Tick(float deltaTime) { }
        }
    }
}
