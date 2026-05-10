using System;
using System.Collections;
using System.IO;
using Cysharp.Threading.Tasks;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using Zero.Core;
using Zero.Services.Save;

namespace Zero.Tests.EditMode
{
    // EncryptedJsonSaveService writes to a fixed file (Application.persistentDataPath/save.dat).
    // Tests therefore run sequentially (NUnit default) and clean up the file in SetUp + TearDown.
    [TestFixture]
    public sealed class SaveServiceTests
    {
        private string _saveFile;

        [SetUp]
        public void SetUp()
        {
            _saveFile = Path.Combine(Application.persistentDataPath, "save.dat");
            DeleteSave();
        }

        [TearDown]
        public void TearDown() => DeleteSave();

        private void DeleteSave()
        {
            if (File.Exists(_saveFile)) File.Delete(_saveFile);
            string tmp = _saveFile + ".tmp";
            if (File.Exists(tmp)) File.Delete(tmp);
        }

        [UnityTest]
        public IEnumerator RoundTripPreservesPrimitivesAndNested() => UniTask.ToCoroutine(async () =>
        {
            var writer = new EncryptedJsonSaveService(new StubLogService());
            await writer.LoadAsync();

            writer.Set("name", "Alice");
            writer.Set("level", 42);
            writer.Set("flags", new[] { "a", "b", "c" });
            await writer.SaveAsync();
            writer.Dispose();

            var reader = new EncryptedJsonSaveService(new StubLogService());
            await reader.LoadAsync();

            Assert.IsTrue(reader.TryGet("name", out string name));
            Assert.AreEqual("Alice", name);

            Assert.IsTrue(reader.TryGet("level", out int level));
            Assert.AreEqual(42, level);

            Assert.IsTrue(reader.TryGet("flags", out string[] flags));
            Assert.AreEqual(new[] { "a", "b", "c" }, flags);

            reader.Dispose();
        });

        [UnityTest]
        public IEnumerator TamperedFileResetsToEmpty() => UniTask.ToCoroutine(async () =>
        {
            var writer = new EncryptedJsonSaveService(new StubLogService());
            await writer.LoadAsync();
            writer.Set("treasure", 9999);
            await writer.SaveAsync();
            writer.Dispose();

            byte[] bytes = File.ReadAllBytes(_saveFile);
            Assert.Greater(bytes.Length, 64, "Save file must include HMAC + IV + ciphertext.");
            // Flip a byte well into the ciphertext (past the 32-byte HMAC and 16-byte IV).
            bytes[60] ^= 0xFF;
            File.WriteAllBytes(_saveFile, bytes);

            var reader = new EncryptedJsonSaveService(new StubLogService());
            // Must not throw — service contract is reset-to-empty on decrypt failure.
            await reader.LoadAsync();
            Assert.IsFalse(reader.TryGet("treasure", out int _),
                "HMAC mismatch should reset to empty; previously-set keys must be unreachable.");
            reader.Dispose();
        });

        [UnityTest]
        public IEnumerator ReloadingExistingFileMergesData() => UniTask.ToCoroutine(async () =>
        {
            // Smoke test for the load → save → reload path. Full migration coverage
            // (writing a synthetic v0 envelope, asserting Migrate(...) ran) requires
            // the service's Migrate hook to be exposed for testing — tracked as a
            // future refactor; see docs/services/save.md "Known Limitations".
            var first = new EncryptedJsonSaveService(new StubLogService());
            await first.LoadAsync();
            first.Set("k", "v");
            await first.SaveAsync();
            first.Dispose();

            var second = new EncryptedJsonSaveService(new StubLogService());
            await second.LoadAsync();
            Assert.IsTrue(second.TryGet("k", out string v));
            Assert.AreEqual("v", v);
            second.Dispose();
        });

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
