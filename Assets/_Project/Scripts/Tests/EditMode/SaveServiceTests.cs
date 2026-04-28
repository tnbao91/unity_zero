using System;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using Zero.Core;
using Zero.Services.Save;

namespace Zero.Tests.EditMode
{
    [TestFixture]
    public sealed class SaveServiceTests
    {
        private string _testSavePath;
        private EncryptedJsonSaveService _service;

        [SetUp]
        public void SetUp()
        {
            // Use a temp file for this test; EncryptedJsonSaveService uses Application.persistentDataPath,
            // so we just clean up the actual persistent file in teardown to avoid side effects.
            _testSavePath = Path.Combine(Application.persistentDataPath, "test_save.dat");
            if (File.Exists(_testSavePath))
            {
                File.Delete(_testSavePath);
            }
            _service = new EncryptedJsonSaveService(new StubLogService());
        }

        [TearDown]
        public void TearDown()
        {
            _service?.Dispose();
            if (File.Exists(_testSavePath))
            {
                File.Delete(_testSavePath);
            }
        }

        [UnityTest]
        public IEnumerator RoundTrip() => UniTask.ToCoroutine(async () =>
        {
            await _service.LoadAsync();

            // Set some data.
            _service.Set("player_name", "Alice");
            _service.Set("level", 42);
            var nestedObj = new { currency = 1000, items = new[] { "sword", "shield" } };
            _service.Set("inventory", nestedObj);

            // Save to disk.
            await _service.SaveAsync();

            // Create a fresh service instance and load from the same file.
            var service2 = new EncryptedJsonSaveService(new StubLogService());
            await service2.LoadAsync();

            // Verify round-trip.
            Assert.IsTrue(service2.TryGet("player_name", out string name) && name == "Alice");
            Assert.IsTrue(service2.TryGet("level", out int level) && level == 42);
            Assert.IsTrue(service2.TryGet("inventory", out dynamic inventory));
            Assert.AreEqual(1000, inventory.currency.Value);
            Assert.AreEqual("sword", inventory.items[0].Value);

            service2.Dispose();
        });

        [UnityTest]
        public IEnumerator TamperDetection() => UniTask.ToCoroutine(async () =>
        {
            await _service.LoadAsync();

            _service.Set("important_data", 9999);
            await _service.SaveAsync();

            // Tamper with the saved file (flip a byte in the middle, past the HMAC and IV).
            string filePath = Path.Combine(Application.persistentDataPath, "save.dat");
            byte[] data = File.ReadAllBytes(filePath);
            Assert.Greater(data.Length, 64);
            data[50] ^= 0xFF; // Flip bits in the middle.
            File.WriteAllBytes(filePath, data);

            // Load from the tampered file. Should NOT throw, but should reset to empty (per contract).
            var service2 = new EncryptedJsonSaveService(new StubLogService());
            await service2.LoadAsync();

            // The tampered data should fail HMAC and reset to empty.
            Assert.IsFalse(service2.TryGet("important_data", out _), "Tampered file should have been reset.");

            service2.Dispose();
        });

        [UnityTest]
        public IEnumerator MigrationWhenLoading() => UniTask.ToCoroutine(async () =>
        {
            // This test verifies that loading a file and triggering migration works.
            // For a full migration test, we'd need to synthesize a v0 file, which requires
            // duplicating the encryption logic. Instead, we verify the happy path: load a v1 file,
            // ensure no error.
            await _service.LoadAsync();
            _service.Set("test_key", "test_value");
            await _service.SaveAsync();

            var service2 = new EncryptedJsonSaveService(new StubLogService());
            // This call should succeed; if migration code breaks, it would throw here.
            await service2.LoadAsync();
            Assert.IsTrue(service2.TryGet("test_key", out string val) && val == "test_value");

            service2.Dispose();
        });

        /// <summary>
        /// Stub ILogService for testing. Implements all methods as no-ops.
        /// </summary>
        private sealed class StubLogService : ILogService
        {
            public void Debug(string message) { }
            public void Info(string message) { }
            public void Warn(string message) { }
            public void Error(string message) { }
            public void Error(Exception ex, string message) { }
        }
    }
}
