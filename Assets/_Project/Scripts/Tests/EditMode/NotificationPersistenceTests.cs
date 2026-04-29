using System;
using System.Collections.Generic;
using System.Threading;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using R3;
using Zero.Core;
using Zero.Services.Notification;

namespace Zero.Tests.EditMode
{
    /// <summary>
    /// Tests notification service permission persistence via ISaveService.
    /// Uses stub save + log services (no actual device notifications).
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

        [Test]
        public async UniTask InitializeAsync_Succeeds()
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();
            // Just verify it doesn't throw
            Assert.Pass();
        }

        [Test]
        public async UniTask RequestPermissionAsync_FirstTime_Grants()
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            bool result = await service.RequestPermissionAsync();
            Assert.IsTrue(result);
        }

        [Test]
        public async UniTask RequestPermissionAsync_Persists_InSaveService()
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            await service.RequestPermissionAsync();

            // Verify persisted
            Assert.IsTrue(_saveService.TryGet("notification.permission.requested", out bool persisted));
            Assert.IsTrue(persisted);
        }

        [Test]
        public async UniTask RequestPermissionAsync_SecondTime_ReturnsCached()
        {
            // First service request permission
            var service1 = new UnityMobileNotificationService(_logService, _saveService);
            await service1.InitializeAsync();
            await service1.RequestPermissionAsync();

            // Second service reads cached state
            var service2 = new UnityMobileNotificationService(_logService, _saveService);
            await service2.InitializeAsync();
            bool result = await service2.RequestPermissionAsync();

            Assert.IsTrue(result);
        }

        [Test]
        public async UniTask Schedule_Logs()
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            service.Schedule("notif-1", "Title", "Body", TimeSpan.FromSeconds(5));
            // Just verify it doesn't throw
            Assert.Pass();
        }

        [Test]
        public async UniTask Cancel_Logs()
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            service.Cancel("notif-1");
            // Just verify it doesn't throw
            Assert.Pass();
        }

        [Test]
        public async UniTask CancelAll_Logs()
        {
            var service = new UnityMobileNotificationService(_logService, _saveService);
            await service.InitializeAsync();

            service.CancelAll();
            // Just verify it doesn't throw
            Assert.Pass();
        }
    }

    /// <summary>In-memory save service for testing.</summary>
    internal sealed class StubSaveService : ISaveService
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

    /// <summary>Stub log service for testing.</summary>
    internal sealed class StubLogService : ILogService
    {
        public void Info(string message) { }
        public void Warn(string message) { }
        public void Error(string message) { }
        public void Fatal(string message) { }
    }
}
