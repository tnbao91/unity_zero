using System;
using System.Collections;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zero.UI;

namespace Zero.Tests.EditMode
{
    public sealed class UITransitionInterruptionTests
    {
        [UnityTest]
        public IEnumerator FadeTransition_Cancellation_DoesNotThrow()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var go = new GameObject("TestCanvas");
                var canvas = go.AddComponent<CanvasGroup>();
                canvas.alpha = 1f;

                using (var cts = new CancellationTokenSource())
                {
                    // Start fade-out
                    var fadeTask = UITransitions.FadeOut(canvas, 0.5f, cts.Token);

                    // Cancel after 100ms
                    await UniTask.Delay(100);
                    cts.Cancel();

                    // Wait for the task to complete
                    try
                    {
                        await fadeTask;
                        Assert.Fail("Expected OperationCanceledException");
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                        Assert.Pass("Cancellation handled correctly");
                    }
                }

                Object.DestroyImmediate(go);
            });
        }

        [UnityTest]
        public IEnumerator SequentialTransitions_AfterCancellation_Works()
        {
            return UniTask.ToCoroutine(async () =>
            {
                var go = new GameObject("TestCanvas");
                var canvas = go.AddComponent<CanvasGroup>();

                // First transition that gets cancelled
                using (var cts = new CancellationTokenSource())
                {
                    var firstFade = UITransitions.FadeIn(canvas, 0.5f, cts.Token);
                    await UniTask.Delay(50);
                    cts.Cancel();

                    try
                    {
                        await firstFade;
                    }
                    catch (OperationCanceledException)
                    {
                        // Expected
                    }
                }

                // Second transition should work without issues
                try
                {
                    await UITransitions.FadeOut(canvas, 0.2f, CancellationToken.None);
                    Assert.AreEqual(0f, canvas.alpha, 0.1f);
                    Assert.Pass("Sequential transitions after cancellation work");
                }
                catch
                {
                    Assert.Fail("Second transition should not throw");
                }

                Object.DestroyImmediate(go);
            });
        }
    }
}
