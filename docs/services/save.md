# Save Service

## Overview

`ISaveService` persists user progress (level state, currencies, settings) to an encrypted file. The implementation `EncryptedJsonSaveService` encrypts the JSON envelope `{version, data}` using AES-256-CBC + HMAC-SHA256, storing it as `[HMAC 32B][IV 16B][ciphertext]`. Per-game encryption seeds are loaded from `Resources/ZeroSecrets.asset` (gitignored). Data is versioned for migrations when formats change.

## Public API

```csharp
public interface ISaveService
{
    Observable<Unit> OnLoaded { get; }
    
    UniTask LoadAsync(CancellationToken ct = default);
    UniTask SaveAsync(CancellationToken ct = default);
    void RequestSave();  // Debounced save (1s window)
    
    bool TryGet<T>(string key, out T value);
    void Set<T>(string key, T value);
    void Delete(string key);
}

// Implementation in Zero.Services.Save
public sealed class EncryptedJsonSaveService : ISaveService, IDisposable { ... }
```

## Extension Points

**Migration callback:** override the internal `Migrate(JObject data, int from, int to)` method to transform old save formats:

```csharp
// In a custom subclass or via patches:
private static JObject Migrate(JObject data, int from, int to)
{
    if (from == 0 && to == 1)
    {
        // v0 used "player_level" key, v1 uses "level"
        data["level"] = data["player_level"];
        data.Remove("player_level");
    }
    return data;
}
```

**Reset behavior:** by design, decryption failure (HMAC mismatch, corrupt ciphertext) does NOT throw; instead, the service logs the error and resets `_data` to empty. This is recoverable for single-slot, casual games. For hardcore economy or leaderboards, implement a `IFailLoudHandler` extension point (future) to throw and block login instead.

## Examples

**Save + load a player profile:**
```csharp
[Inject] private ISaveService _save;

public async UniTask SaveProfileAsync(string playerName, int level, CancellationToken ct)
{
    _save.Set("player_name", playerName);
    _save.Set("level", level);
    await _save.SaveAsync(ct);
}

public async UniTask LoadProfileAsync(CancellationToken ct)
{
    await _save.LoadAsync(ct);
    if (_save.TryGet("player_name", out string name))
    {
        _playerName = name;
    }
}
```

**Debounced save on setting changes:**
```csharp
// In game logic that updates frequently (every frame)
void Update()
{
    _save.Set("player_x", transform.position.x);
    _save.Set("player_y", transform.position.y);
    _save.RequestSave();  // Debounced; saves at most once per 1s
}
```

**Load notification:**
```csharp
_save.OnLoaded.Subscribe(_ => 
{
    // Now safe to read from _save
    if (_save.TryGet("session_token", out string token))
    {
        _sessionManager.RestoreSession(token);
    }
});
```

## Known Limitations

- **Single-slot only:** the template saves to one file. Multi-profile save systems must be consumer-built on top of `ISaveService` (e.g., enumerate multiple files).
- **Client-side crypto:** HMAC protects against casual tampering (modifying values in a hex editor). It does NOT protect against reverse-engineering the secret or replaying old saves. If progress or currency is tied to real-world money, **you must validate on the backend** (server-of-truth).
- **No compression:** JSON is stored plaintext → ciphertext, uncompressed. Large save files (>1MB) will be slow to encrypt; consider archiving non-essential data.
- **Synchronous access:** `TryGet` / `Set` / `Delete` are synchronous; they lock a mutex internally. For very frequent updates (10000+/frame), batch writes in a temporary dict and call `Set` once per frame instead.

## Design Rationale

**Why AES-CBC + HMAC?** AES is the standard symmetric cipher (no key exchange needed). HMAC-SHA256 provides authenticated encryption (tamper detection). Together they offer "encrypt-then-MAC" which is more secure than raw AES. This protects casual editing without the complexity of public-key crypto.

**Why a JSON envelope?** Version field allows format evolution. The `{version, data}` structure is human-readable (for debugging saves in plaintext) and plays well with Newtonsoft.Json migrations.

**Why gitignore `ZeroSecrets.asset`?** Per-game secrets must never land in version control. The consumer copies `ZeroSecrets.asset.example` into place and fills in per-game seeds (e.g., a unique string per game build).

**Encryption seed source:** seeds are now loaded from `Resources/ZeroSecrets.asset` (created by user). Editor builds fall back to hardcoded template defaults with a loud warning. Player builds throw if the asset is missing.
