using System.Threading;
using Cysharp.Threading.Tasks;
using UnityEngine;

namespace Zero.Core
{
    public interface IPool<T> where T : UnityEngine.Object
    {
        int Active { get; }
        int Inactive { get; }
        T Spawn(Vector3 position, Quaternion rotation);
        T Spawn();
        void Despawn(T instance);
    }

    public interface IPoolService
    {
        UniTask PrewarmAsync<T>(T prefab, int count, CancellationToken ct = default) where T : UnityEngine.Object;
        IPool<T> GetPool<T>(T prefab) where T : UnityEngine.Object;
        void Clear<T>(T prefab) where T : UnityEngine.Object;
    }
}
