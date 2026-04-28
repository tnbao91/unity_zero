using System;
using System.Collections;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Services.Pool;

namespace Zero.Tests.EditMode
{
    [TestFixture]
    public sealed class PoolServiceTests
    {
        private UnityPoolService _service;
        private GameObject _prefab;

        [SetUp]
        public void SetUp()
        {
            _service = new UnityPoolService(new StubLogService());
            _prefab = new GameObject("TestPrefab");
            _prefab.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            if (_prefab != null) UnityEngine.Object.DestroyImmediate(_prefab);
        }

        [Test]
        public void GetReleaseLifoOrdering()
        {
            var pool = _service.GetPool(_prefab);
            var a = pool.Spawn();
            var b = pool.Spawn();
            var c = pool.Spawn();
            Assert.AreEqual(3, pool.Active);

            pool.Despawn(b);
            Assert.AreEqual(2, pool.Active);
            Assert.AreEqual(1, pool.Inactive);

            // Next spawn should reuse `b` (LIFO from UnityEngine.Pool.ObjectPool's Stack).
            var d = pool.Spawn();
            Assert.AreSame(b, d, "Pool should reuse the most recently released instance.");
        }

        [UnityTest]
        public IEnumerator PrewarmFillsInactive() => UniTask.ToCoroutine(async () =>
        {
            await _service.PrewarmAsync(_prefab, 5);

            var pool = _service.GetPool(_prefab);
            Assert.AreEqual(0, pool.Active, "Prewarm must not leak Active count.");
            Assert.AreEqual(5, pool.Inactive, "Prewarm should populate Inactive with N instances.");
        });

        [Test]
        public void SpawnedObjectIsActiveDespawnedIsInactive()
        {
            var pool = _service.GetPool(_prefab);
            var go = pool.Spawn(new Vector3(1, 2, 3), Quaternion.identity);
            Assert.IsTrue(go.activeSelf, "Spawned GameObject must be active.");
            Assert.AreEqual(new Vector3(1, 2, 3), go.transform.position);

            pool.Despawn(go);
            Assert.IsFalse(go.activeSelf, "Despawned GameObject must be inactive.");
        }

        [Test]
        public void DistinctSpawnsReturnDistinctInstances()
        {
            var pool = _service.GetPool(_prefab);
            var a = pool.Spawn();
            var b = pool.Spawn();
            var c = pool.Spawn();
            Assert.AreNotSame(a, b);
            Assert.AreNotSame(b, c);
            Assert.AreNotSame(a, c);
        }

        [Test]
        public void DisposeIsIdempotent()
        {
            var pool = _service.GetPool(_prefab);
            var go = pool.Spawn();
            pool.Despawn(go);

            Assert.DoesNotThrow(() => _service.Dispose());
            Assert.DoesNotThrow(() => _service.Dispose());
        }

        private sealed class StubLogService : ILogService
        {
            public bool IsEnabled { get; set; } = true;
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string context = null) { }
        }
    }
}
