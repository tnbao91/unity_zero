using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Services.Notification;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Tests notification service caching + persistence behavior using stub save/log services.
    /// Real Unity.Notifications APIs are wrapped in defensive try/catch by the service, so
    /// EditMode-on-macOS exercises the C# logic (cache load, persistence, ifdef paths) without
    /// requiring a device. Permission grant on EditMode is platform-dependent and intentionally
    /// not asserted; manual checklist covers device verification.
    /// </summary>
    public sealed class NotificationPersistenceTests
    {
        private StubSaveService _saveService;
        private StubLogService _logService;

        [SetUp]
        public void Setup()
        {
            _saveService = new StubSaveService();
            _logService = new StubLogService();
        }

        [UnityTest]
        public IEnumerator InitializeAsync_DoesNotThrow() => UniTask.ToCoroutine(async () =>
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();
            Assert.Pass();
        });

        [UnityTest]
        public IEnumerator InitializeAsync_LoadsCachedPermissionFromSave() => UniTask.ToCoroutine(async () =>
        {
            // Pre-seed save service with a previous grant.
            _saveService.Set("notification.permission.requested", true);

            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            // Cached path: RequestPermission should short-circuit and return true without calling
            // any Unity API (verified by the absence of an exception even on platforms where the
            // real RequestPermission would fail).
            bool result = await service.RequestPermissionAsync();
            Assert.IsTrue(result, "Cached permission should short-circuit to granted=true.");
        });

        [UnityTest]
        public IEnumerator RequestPermissionAsync_SecondCall_UsesCachedState() => UniTask.ToCoroutine(async () =>
        {
            // Seed the save service so the first init path treats permission as already granted.
            _saveService.Set("notification.permission.requested", true);

            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            // Two calls should both return true via cache; neither should hit the native API.
            bool first = await service.RequestPermissionAsync();
            bool second = await service.RequestPermissionAsync();

            Assert.IsTrue(first);
            Assert.IsTrue(second);
        });

        [UnityTest]
        public IEnumerator Schedule_DoesNotThrow() => UniTask.ToCoroutine(async () =>
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            // Service wraps NotificationCenter.ScheduleNotification in try/catch, so this is
            // safe to invoke even when the native subsystem isn't available (e.g., macOS Editor).
            service.Schedule("notif-1", "Title", "Body", TimeSpan.FromSeconds(5));
            Assert.Pass();
        });

        [UnityTest]
        public IEnumerator Cancel_UnknownId_DoesNotThrow() => UniTask.ToCoroutine(async () =>
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            service.Cancel("never-scheduled");
            Assert.Pass();
        });

        [UnityTest]
        public IEnumerator CancelAll_DoesNotThrow() => UniTask.ToCoroutine(async () =>
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            service.CancelAll();
            Assert.Pass();
        });

        private sealed class StubLogService : ILogService
        {
            public bool IsEnabled { get; set; } = true;
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception exception, string context = null) { }
        }

        private sealed class StubSaveService : ISaveService
        {
            private readonly Dictionary<string, object> _data = new();
            private readonly Subject<Unit> _onLoaded = new();

            public Observable<Unit> OnLoaded => _onLoaded;

            public UniTask LoadAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public UniTask SaveAsync(CancellationToken ct = default) => UniTask.CompletedTask;
            public void RequestSave() { }

            public bool TryGet<T>(string key, out T value)
            {
                if (_data.TryGetValue(key, out var obj) && obj is T typedValue)
                {
                    value = typedValue;
                    return true;
                }
                value = default;
                return false;
            }

            public void Set<T>(string key, T value) => _data[key] = value;
            public void Delete(string key) => _data.Remove(key);
        }
    }
}
