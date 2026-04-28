using R3;

namespace Zero.Core
{
    // Type-keyed pub/sub bus for cross-asmdef communication.
    // Subscribers asmdef tham chiếu Zero.Core only — they need not reference the publisher.
    public interface IEventBus
    {
        Observable<T> On<T>();
        void Publish<T>(T evt);
    }
}
