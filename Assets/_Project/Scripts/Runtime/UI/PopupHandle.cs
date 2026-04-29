using System.Threading;
using Cysharp.Threading.Tasks;

namespace Zero.UI
{
    /// <summary>
    /// Generic handle returned by UIService.PushAsync<TPopup, TData, TResult>.
    /// Allows the caller to wait for the popup to close and retrieve the result.
    /// </summary>
    public sealed class PopupHandle<TResult>
    {
        private readonly UniTaskCompletionSource<TResult> _completionSource = new();

        public UniTask<TResult> Result => _completionSource.Task;

        internal void Close(TResult result)
        {
            _completionSource.TrySetResult(result);
        }

        internal void Cancel()
        {
            _completionSource.TrySetCanceled();
        }
    }
}
