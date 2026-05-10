using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.UI
{
    /// <summary>
    /// Non-generic interface for popup handle cancellation.
    /// Allows UIService.PopAsync to cancel handles without dynamic dispatch (IL2CPP compatible).
    /// </summary>
    internal interface IPopupHandle
    {
        void Cancel();
    }

    /// <summary>
    /// Generic handle returned by UIService.PushAsync<TPopup, TData, TResult>.
    /// Allows the caller to wait for the popup to close and retrieve the result.
    /// </summary>
    public sealed class PopupHandle<TResult> : IPopupHandle
    {
        private readonly UniTaskCompletionSource<TResult> _completionSource = new();

        public UniTask<TResult> Result => _completionSource.Task;

        internal void Close(TResult result)
        {
            _completionSource.TrySetResult(result);
        }

        void IPopupHandle.Cancel()
        {
            _completionSource.TrySetCanceled();
        }
    }
}
