using System;
using System.Collections.Generic;
using UnityEngine;

namespace Zero.UI
{
    /// <summary>
    /// Internal stack container for managing popup instances.
    /// Tracks popup order, assigns sort orders within a layer, and manages queued requests.
    /// </summary>
    internal sealed class PopupStack
    {
        private readonly Queue<GameObject> _queue = new();
        // List-as-stack (end = top): TryRemove must evict a specific popup's
        // entry by reference when its push is cancelled or it closes while no
        // longer on top — Stack.Pop() would evict whichever popup is topmost
        // (same bug shape as UIService._activePopups, fixed together in Phase 6).
        private readonly List<PopupEntry> _stack = new();
        private int _sortOrderOffset;

        public int Count => _stack.Count;
        public int QueuedCount => _queue.Count;

        public struct PopupEntry
        {
            public GameObject Instance;
            public int AssignedSortOrder;
        }

        public int Push(GameObject popupInstance, int layerBaseSortOrder)
        {
            var sortOrder = layerBaseSortOrder + _sortOrderOffset;
            var entry = new PopupEntry
            {
                Instance = popupInstance,
                AssignedSortOrder = sortOrder
            };
            _stack.Add(entry);
            _sortOrderOffset++;
            return sortOrder;
        }

        public bool TryPop(out GameObject popupInstance)
        {
            if (_stack.Count > 0)
            {
                var entry = _stack[_stack.Count - 1];
                _stack.RemoveAt(_stack.Count - 1);
                popupInstance = entry.Instance;
                _sortOrderOffset--;
                return true;
            }

            popupInstance = null;
            return false;
        }

        /// <summary>
        /// Remove a specific popup's entry by reference, wherever it sits in the
        /// stack. Returns false when the instance was never pushed (e.g. a popup
        /// without a Canvas) — callers treat that as a no-op.
        /// </summary>
        public bool TryRemove(GameObject popupInstance)
        {
            for (int i = _stack.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(_stack[i].Instance, popupInstance))
                {
                    _stack.RemoveAt(i);
                    _sortOrderOffset--;
                    return true;
                }
            }

            return false;
        }

        public bool TryPeek(out GameObject popupInstance)
        {
            if (_stack.Count > 0)
            {
                popupInstance = _stack[_stack.Count - 1].Instance;
                return true;
            }

            popupInstance = null;
            return false;
        }

        /// <summary>
        /// Peek the sort order that the next push would receive.
        /// Used by backdrop creation to ensure backdrop sorts behind the popup.
        /// </summary>
        public int PeekNextSortOrder(int layerBaseSortOrder)
        {
            return layerBaseSortOrder + _sortOrderOffset;
        }

        public bool TryReplace(GameObject newPopupInstance, int layerBaseSortOrder, out GameObject oldInstance)
        {
            if (_stack.Count > 0)
            {
                var oldEntry = _stack[_stack.Count - 1];
                oldInstance = oldEntry.Instance;

                _stack[_stack.Count - 1] = new PopupEntry
                {
                    Instance = newPopupInstance,
                    AssignedSortOrder = layerBaseSortOrder + (_sortOrderOffset - 1)
                };
                return true;
            }

            oldInstance = null;
            return false;
        }

        public void QueuePush(GameObject popupInstance)
        {
            _queue.Enqueue(popupInstance);
        }

        public bool TryDequeuePush(out GameObject popupInstance)
        {
            return _queue.TryDequeue(out popupInstance);
        }

        public void Clear()
        {
            _stack.Clear();
            _queue.Clear();
            _sortOrderOffset = 0;
        }
    }
}
