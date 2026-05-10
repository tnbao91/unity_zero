using System;
using System.Collections.Generic;
using R3;
using Zero.Core;

namespace Zero.Services.Events
{
    // Type-keyed pub/sub backed by R3 Subjects. Storage type is Dictionary<Type, object>
    // (cast to Subject<T> at access). We deliberately avoid Dictionary<Type, Subject<object>>
    // — that would box value-type events at every call.
    //
    // Bus is a R3 Subject under the hood: synchronous dispatch, single-threaded by Unity
    // convention. Lock guards against rare DI-driven background access (e.g. an async
    // service publishing during init). Subscribers receive on the same context they
    // subscribed from (R3 default).
    public sealed class R3EventBus : IEventBus, IDisposable
    {
        private readonly Dictionary<Type, object> _subjects = new();
        private bool _disposed;

        public Observable<T> On<T>()
        {
            lock (_subjects)
            {
                ThrowIfDisposed();
                if (!_subjects.TryGetValue(typeof(T), out var s))
                {
                    s = new Subject<T>();
                    _subjects[typeof(T)] = s;
                }
                return (Subject<T>)s;
            }
        }

        public void Publish<T>(T evt)
        {
            Subject<T> subject;
            lock (_subjects)
            {
                ThrowIfDisposed();
                if (!_subjects.TryGetValue(typeof(T), out var s))
                {
                    // No subscribers yet; create the subject lazily so future On<T>() returns
                    // the same instance and stays consistent with subscription behavior.
                    s = new Subject<T>();
                    _subjects[typeof(T)] = s;
                }
                subject = (Subject<T>)s;
            }
            subject.OnNext(evt);
        }

        public void Dispose()
        {
            lock (_subjects)
            {
                if (_disposed) return;
                _disposed = true;
                foreach (var s in _subjects.Values)
                {
                    if (s is IDisposable d) d.Dispose();
                }
                _subjects.Clear();
            }
        }

        private void ThrowIfDisposed()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(R3EventBus));
        }
    }
}
