using NUnit.Framework;
using UnityEngine;
using Zero.Core;
using Zero.UI;

namespace Zero.Tests.EditMode
{
    public sealed class PopupStackTests
    {
        [Test]
        public void Push_TwoPopups_TopIsSecond()
        {
            var stack = new PopupStack();
            var go1 = new GameObject("Popup1");
            var go2 = new GameObject("Popup2");

            stack.Push(go1, (int)Core.UiLayer.Popup);
            stack.Push(go2, (int)Core.UiLayer.Popup);

            Assert.IsTrue(stack.TryPeek(out var top));
            Assert.AreEqual(go2, top);

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void Pop_TwoPopups_ReturnsInReverseOrder()
        {
            var stack = new PopupStack();
            var go1 = new GameObject("Popup1");
            var go2 = new GameObject("Popup2");

            stack.Push(go1, (int)Core.UiLayer.Popup);
            stack.Push(go2, (int)Core.UiLayer.Popup);

            Assert.IsTrue(stack.TryPop(out var popped1));
            Assert.AreEqual(go2, popped1);

            Assert.IsTrue(stack.TryPop(out var popped2));
            Assert.AreEqual(go1, popped2);

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void Replace_StackHasOne_ReplacesTop()
        {
            var stack = new PopupStack();
            var go1 = new GameObject("Popup1");
            var go2 = new GameObject("Popup2");

            stack.Push(go1, (int)Core.UiLayer.Popup);
            bool replaced = stack.TryReplace(go2, (int)Core.UiLayer.Popup, out var old);

            Assert.IsTrue(replaced);
            Assert.AreEqual(go1, old);
            Assert.IsTrue(stack.TryPeek(out var top));
            Assert.AreEqual(go2, top);
            Assert.AreEqual(1, stack.Count);

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
        }

        [Test]
        public void SortOrder_MonotonicallyIncreasing()
        {
            var stack = new PopupStack();
            var go1 = new GameObject("Popup1");
            var go2 = new GameObject("Popup2");
            var go3 = new GameObject("Popup3");

            int sort1 = stack.Push(go1, (int)Core.UiLayer.Popup);
            int sort2 = stack.Push(go2, (int)Core.UiLayer.Popup);
            int sort3 = stack.Push(go3, (int)Core.UiLayer.Popup);

            Assert.Less(sort1, sort2);
            Assert.Less(sort2, sort3);

            Object.DestroyImmediate(go1);
            Object.DestroyImmediate(go2);
            Object.DestroyImmediate(go3);
        }

        [Test]
        public void QueuePush_EnqueueAndDequeue()
        {
            var stack = new PopupStack();
            var go1 = new GameObject("Popup1");

            stack.QueuePush(go1);
            Assert.AreEqual(1, stack.QueuedCount);

            Assert.IsTrue(stack.TryDequeuePush(out var dequeued));
            Assert.AreEqual(go1, dequeued);
            Assert.AreEqual(0, stack.QueuedCount);

            Object.DestroyImmediate(go1);
        }
    }
}
