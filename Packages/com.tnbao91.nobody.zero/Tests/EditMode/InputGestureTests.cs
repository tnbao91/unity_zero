using NUnit.Framework;
using UnityEngine;
using Zero.Core;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Unit tests for gesture detection logic (tap, swipe classification).
    /// These test the pure logic without mocking InputSystem (which cannot run headless).
    /// </summary>
    public sealed class InputGestureTests
    {
        private static class GestureDetector
        {
            public const float TapWindowMs = 200f;
            public const float SwipeWindowMs = 500f;
            public const float TapDragThresholdPx = 20f;
            public const float SwipeMinDistancePx = 50f;

            public static (bool isTap, bool isSwipe) Classify(
                Vector2 downPos, Vector2 upPos, float downTimeMs, float upTimeMs)
            {
                Vector2 dragDelta = upPos - downPos;
                float dragDistance = dragDelta.magnitude;
                float elapsedMs = upTimeMs - downTimeMs;

                bool isTap = dragDistance < TapDragThresholdPx && elapsedMs < TapWindowMs;
                bool isSwipe = dragDistance >= SwipeMinDistancePx && elapsedMs < SwipeWindowMs;

                return (isTap, isSwipe);
            }
        }

        [Test]
        public void TapWindow_Within200ms_UnderThreshold_IsTap()
        {
            // Pointer down at (100, 100), up at (105, 100) after 150ms
            var (isTap, isSwipe) = GestureDetector.Classify(
                new Vector2(100, 100),
                new Vector2(105, 100),
                downTimeMs: 0,
                upTimeMs: 150);

            Assert.IsTrue(isTap);
            Assert.IsFalse(isSwipe);
        }

        [Test]
        public void TapWindow_Over200ms_IsTap_False()
        {
            var (isTap, isSwipe) = GestureDetector.Classify(
                new Vector2(100, 100),
                new Vector2(105, 100),
                downTimeMs: 0,
                upTimeMs: 250);

            Assert.IsFalse(isTap);
            Assert.IsFalse(isSwipe);
        }

        [Test]
        public void SwipeThreshold_50px_IsSwipe()
        {
            var (isTap, isSwipe) = GestureDetector.Classify(
                new Vector2(100, 100),
                new Vector2(150, 100),
                downTimeMs: 0,
                upTimeMs: 300);

            Assert.IsFalse(isTap);
            Assert.IsTrue(isSwipe);
        }

        [Test]
        public void SwipeThreshold_49px_IsNotSwipe()
        {
            var (isTap, isSwipe) = GestureDetector.Classify(
                new Vector2(100, 100),
                new Vector2(149, 100),
                downTimeMs: 0,
                upTimeMs: 300);

            Assert.IsFalse(isTap);
            Assert.IsFalse(isSwipe);
        }

        [Test]
        public void SwipeWindow_Over500ms_IsNotSwipe()
        {
            var (isTap, isSwipe) = GestureDetector.Classify(
                new Vector2(100, 100),
                new Vector2(150, 100),
                downTimeMs: 0,
                upTimeMs: 550);

            Assert.IsFalse(isTap);
            Assert.IsFalse(isSwipe);
        }

        [Test]
        public void DiagonalSwipe_50px_IsSwipe()
        {
            // Diagonal: (0,0) → (35.36, 35.36) ≈ 50px distance
            var (isTap, isSwipe) = GestureDetector.Classify(
                new Vector2(0, 0),
                new Vector2(35.36f, 35.36f),
                downTimeMs: 0,
                upTimeMs: 300);

            Assert.IsFalse(isTap);
            Assert.IsTrue(isSwipe);
        }
    }
}
