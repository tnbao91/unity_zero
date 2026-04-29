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
        private readonly Queue<PopupEntry> _queue = new();
        private readonly Stack<PopupEntry> _stack = new();
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
            _stack.Push(entry);
            _sortOrderOffset++;
            return sortOrder;
        }

        public bool TryPop(out GameObject popupInstance)
        {
            if (_stack.Count > 0)
            {
                var entry = _stack.Pop();
                popupInstance = entry.Instance;
                _sortOrderOffset--;
                return true;
            }

            popupInstance = null;
            return false;
        }

        public bool TryPeek(out GameObject popupInstance)
        {
            if (_stack.Count > 0)
            {
                popupInstance = _stack.Peek().Instance;
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
                var oldEntry = _stack.Pop();
                oldInstance = oldEntry.Instance;

                var newEntry = new PopupEntry
                {
                    Instance = newPopupInstance,
                    AssignedSortOrder = layerBaseSortOrder + (_sortOrderOffset - 1)
                };
                _stack.Push(newEntry);
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
