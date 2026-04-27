using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Cysharp.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using R3;
using UnityEngine;
using Zero.Core;

namespace Zero.Services.Save
{
    public sealed class EncryptedJsonSaveService : ISaveService, IDisposable
    {
        private const int CurrentVersion = 1;
        private const string FileName = "save.dat";
        private const int DebounceMs = 1000;
        private const int IvSize = 16;
        private const int MacSize = 32;

        // IMPORTANT: replace these constants with per-game secrets before shipping.
        private static readonly byte[] DefaultAesSeed = Encoding.UTF8.GetBytes("ZeroTemplate.Aes.SeedV1");
        private static readonly byte[] DefaultHmacSeed = Encoding.UTF8.GetBytes("ZeroTemplate.Hmac.SeedV1");

        private readonly ILogService _log;
        private readonly string _filePath;
        private readonly byte[] _aesKey;
        private readonly byte[] _hmacKey;
        private readonly SemaphoreSlim _ioLock = new(1, 1);
        private readonly Subject<Unit> _onLoaded = new();
        private readonly object _dataLock = new();

        private JObject _data = new();
        private CancellationTokenSource _debounceCts;
        private bool _disposed;

        public Observable<Unit> OnLoaded => _onLoaded;

        public EncryptedJsonSaveService(ILogService log)
        {
            _log = log;
            _filePath = Path.Combine(Application.persistentDataPath, FileName);
            _aesKey = DeriveKey(DefaultAesSeed);
            _hmacKey = DeriveKey(DefaultHmacSeed);
        }

        public async UniTask LoadAsync(CancellationToken ct = default)
        {
            await _ioLock.WaitAsync(ct);
            try
            {
                if (!File.Exists(_filePath))
                {
                    _log.Info($"[SAVE] No save file at {_filePath} - using defaults");
                    lock (_dataLock) { _data = new JObject(); }
                    _onLoaded.OnNext(Unit.Default);
                    return;
                }

                await UniTask.SwitchToThreadPool();
                byte[] raw = File.ReadAllBytes(_filePath);
                string json;
                try
                {
                    json = Decrypt(raw, _aesKey, _hmacKey);
                }
                catch (Exception ex)
                {
                    await UniTask.SwitchToMainThread();
                    _log.Error(ex, "[SAVE] Decryption failed; resetting to empty");
                    lock (_dataLock) { _data = new JObject(); }
                    _onLoaded.OnNext(Unit.Default);
                    return;
                }

                JObject envelope;
                try
                {
                    envelope = JObject.Parse(json);
                }
                catch (Exception ex)
                {
                    await UniTask.SwitchToMainThread();
                    _log.Error(ex, "[SAVE] JSON parse failed; resetting to empty");
                    lock (_dataLock) { _data = new JObject(); }
                    _onLoaded.OnNext(Unit.Default);
                    return;
                }

                int version = envelope.TryGetValue("version", out var v) ? v.Value<int>() : 0;
                JObject payload = envelope["data"] as JObject ?? new JObject();
                if (version != CurrentVersion)
                {
                    payload = Migrate(payload, version, CurrentVersion);
                }

                await UniTask.SwitchToMainThread();
                lock (_dataLock) { _data = payload; }
                _log.Info($"[SAVE] Loaded {payload.Count} keys (v{version} -> v{CurrentVersion})");
                _onLoaded.OnNext(Unit.Default);
            }
            finally
            {
                _ioLock.Release();
            }
        }

        public async UniTask SaveAsync(CancellationToken ct = default)
        {
            await _ioLock.WaitAsync(ct);
            try
            {
                JObject snapshot;
                lock (_dataLock) { snapshot = (JObject)_data.DeepClone(); }

                var envelope = new JObject
                {
                    ["version"] = CurrentVersion,
                    ["data"] = snapshot,
                };
                string json = envelope.ToString(Formatting.None);

                await UniTask.SwitchToThreadPool();
                byte[] cipher = Encrypt(json, _aesKey, _hmacKey);
                string tmp = _filePath + ".tmp";
                File.WriteAllBytes(tmp, cipher);
                if (File.Exists(_filePath))
                {
                    File.Replace(tmp, _filePath, null);
                }
                else
                {
                    File.Move(tmp, _filePath);
                }

                await UniTask.SwitchToMainThread();
                _log.Info($"[SAVE] Persisted {snapshot.Count} keys ({cipher.Length} bytes)");
            }
            finally
            {
                _ioLock.Release();
            }
        }

        public void RequestSave()
        {
            if (_disposed) return;
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _debounceCts = new CancellationTokenSource();
            DebouncedSaveAsync(_debounceCts.Token).Forget();
        }

        private async UniTaskVoid DebouncedSaveAsync(CancellationToken ct)
        {
            try
            {
                await UniTask.Delay(DebounceMs, cancellationToken: ct);
                await SaveAsync(ct);
            }
            catch (OperationCanceledException) { /* superseded */ }
            catch (Exception ex)
            {
                _log.Error(ex, "[SAVE] Debounced save failed");
            }
        }

        public bool TryGet<T>(string key, out T value)
        {
            lock (_dataLock)
            {
                if (_data.TryGetValue(key, out var token) && token != null && token.Type != JTokenType.Null)
                {
                    try
                    {
                        value = token.ToObject<T>();
                        return true;
                    }
                    catch
                    {
                        value = default;
                        return false;
                    }
                }
            }
            value = default;
            return false;
        }

        public void Set<T>(string key, T value)
        {
            lock (_dataLock)
            {
                _data[key] = value == null ? JValue.CreateNull() : JToken.FromObject(value);
            }
        }

        public void Delete(string key)
        {
            lock (_dataLock)
            {
                _data.Remove(key);
            }
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _debounceCts?.Cancel();
            _debounceCts?.Dispose();
            _ioLock.Dispose();
            _onLoaded.Dispose();
        }

        private static JObject Migrate(JObject data, int from, int to)
        {
            // Per-game migrations go here. v1 template is no-op.
            return data;
        }

        private static byte[] DeriveKey(byte[] seed)
        {
            using var sha = SHA256.Create();
            return sha.ComputeHash(seed);
        }

        private static byte[] Encrypt(string plainText, byte[] aesKey, byte[] hmacKey)
        {
            byte[] plain = Encoding.UTF8.GetBytes(plainText);

            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            aes.GenerateIV();
            byte[] iv = aes.IV;

            byte[] cipher;
            using (var enc = aes.CreateEncryptor())
            {
                cipher = enc.TransformFinalBlock(plain, 0, plain.Length);
            }

            byte[] ivAndCipher = new byte[iv.Length + cipher.Length];
            Buffer.BlockCopy(iv, 0, ivAndCipher, 0, iv.Length);
            Buffer.BlockCopy(cipher, 0, ivAndCipher, iv.Length, cipher.Length);

            byte[] mac;
            using (var hmac = new HMACSHA256(hmacKey))
            {
                mac = hmac.ComputeHash(ivAndCipher);
            }

            byte[] result = new byte[mac.Length + ivAndCipher.Length];
            Buffer.BlockCopy(mac, 0, result, 0, mac.Length);
            Buffer.BlockCopy(ivAndCipher, 0, result, mac.Length, ivAndCipher.Length);
            return result;
        }

        private static string Decrypt(byte[] data, byte[] aesKey, byte[] hmacKey)
        {
            if (data.Length < MacSize + IvSize)
            {
                throw new InvalidDataException("Save file too short");
            }

            byte[] mac = new byte[MacSize];
            Buffer.BlockCopy(data, 0, mac, 0, MacSize);
            byte[] ivAndCipher = new byte[data.Length - MacSize];
            Buffer.BlockCopy(data, MacSize, ivAndCipher, 0, ivAndCipher.Length);

            byte[] computed;
            using (var hmac = new HMACSHA256(hmacKey))
            {
                computed = hmac.ComputeHash(ivAndCipher);
            }

            if (!ConstantTimeEquals(mac, computed))
            {
                throw new InvalidDataException("Save integrity check failed (HMAC mismatch)");
            }

            byte[] iv = new byte[IvSize];
            Buffer.BlockCopy(ivAndCipher, 0, iv, 0, IvSize);
            byte[] cipher = new byte[ivAndCipher.Length - IvSize];
            Buffer.BlockCopy(ivAndCipher, IvSize, cipher, 0, cipher.Length);

            using var aes = Aes.Create();
            aes.Key = aesKey;
            aes.IV = iv;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;

            byte[] plain;
            using (var dec = aes.CreateDecryptor())
            {
                plain = dec.TransformFinalBlock(cipher, 0, cipher.Length);
            }
            return Encoding.UTF8.GetString(plain);
        }

        private static bool ConstantTimeEquals(byte[] a, byte[] b)
        {
            if (a.Length != b.Length) return false;
            int diff = 0;
            for (int i = 0; i < a.Length; i++) diff |= a[i] ^ b[i];
            return diff == 0;
        }
    }
}
