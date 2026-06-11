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

**Migrations.** `EncryptedJsonSaveService` is `sealed` and `Migrate(JObject, int from, int to)` is `private static`, so per-game migrations are written by editing that method directly. v1 ships as a no-op:

```csharp
private static JObject Migrate(JObject data, int from, int to)
{
    if (from == 0 && to == 1)
    {
        // v0 used "player_level"; v1 uses "level".
        if (data["player_level"] != null)
        {
            data["level"] = data["player_level"];
            data.Remove("player_level");
        }
    }
    return data;
}
```

If your game needs migration coverage in tests (write a v0 envelope, assert the hook fires), the template-side refactor is an injected `ISaveMigrator` seam — not subclassing; the class stays `sealed`. That's a deliberate v1 limitation, not an accidental one. See "Known Limitations" below.

**Reset-on-decrypt-fail (with quarantine).** Decryption failure (HMAC mismatch, corrupt ciphertext) does NOT throw. The service first moves the unreadable file to `save.dat.corrupt` (overwriting any previous quarantine), then logs and resets the in-memory `_data` to empty so the next launch gets a fresh save. The quarantined file is the recovery/forensics seam: a support flow can upload it, a patched build can re-attempt decryption, or QA can diff it. For games where progress is monetised, replace `EncryptedJsonSaveService` entirely with an impl that fails loudly and blocks launch until a server-side recovery flow runs — `ISaveService` lives in `Zero.Core` precisely so consumers can swap impls without touching call sites.

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

**Mobile lifecycle flush (REQUIRED for production mobile):**
```csharp
// Attach to any long-lived MonoBehaviour in your game.
// On iOS/Android the OS pauses then KILLS the app — Dispose() may never run.
// SaveAsync bypasses the debounce, so this persists everything immediately.
private void OnApplicationPause(bool pauseStatus)
{
    if (pauseStatus)
    {
        _save.SaveAsync().Forget();
    }
}
```

`Dispose()` is the desktop/editor safety net: if a `RequestSave` is still inside the 1s debounce window when the service is disposed (app quit), it flushes synchronously before releasing resources. The pause hook above remains the primary mechanism on mobile, because suspended apps are killed without any C# callback.

## Known Limitations

- **Single-slot only:** the template saves to one file. Multi-profile save systems must be consumer-built on top of `ISaveService` (e.g., enumerate multiple files).
- **Client-side crypto:** HMAC protects against casual tampering (modifying values in a hex editor). It does NOT protect against reverse-engineering the secret or replaying old saves. If progress or currency is tied to real-world money, **you must validate on the backend** (server-of-truth).
- **No compression:** JSON is stored plaintext → ciphertext, uncompressed. Large save files (>1MB) will be slow to encrypt; consider archiving non-essential data.
- **Synchronous access:** `TryGet` / `Set` / `Delete` are synchronous; they lock a mutex internally. For very frequent updates (10000+/frame), batch writes in a temporary dict and call `Set` once per frame instead.
- **Quarantine is single-slot:** only the most recent corrupt file is kept (`save.dat.corrupt` is overwritten by a newer corruption). A support/recovery flow that needs history must copy it elsewhere.
- **Dispose flush is best-effort:** if an explicit `SaveAsync` is mid-flight when `Dispose()` runs, the flush is skipped (that save already carries a snapshot at least as fresh as the debounce). On mobile, rely on the `OnApplicationPause` recipe above, not on `Dispose()`.
- **Migration testing.** v1's `Migrate` is `private static` and the class is `sealed`, so the EditMode test suite cannot write a synthetic v0 file and assert the callback fires end-to-end. The shipped tests cover round-trip and tamper-reset only. Promoting `Migrate` to `protected virtual` (or refactoring it behind an injected `ISaveMigrator` interface) is a candidate refactor when a real game adds its first migration.

## Design Rationale

**Why AES-CBC + HMAC?** AES is the standard symmetric cipher (no key exchange needed). HMAC-SHA256 provides authenticated encryption (tamper detection). Together they offer "encrypt-then-MAC" which is more secure than raw AES. This protects casual editing without the complexity of public-key crypto.

**Why a JSON envelope?** Version field allows format evolution. The `{version, data}` structure is human-readable (for debugging saves in plaintext) and plays well with Newtonsoft.Json migrations.

**Why gitignore `ZeroSecrets.asset`?** Per-game secrets must never land in version control. The consumer copies `ZeroSecrets.asset.example` into place and fills in per-game seeds (e.g., a unique string per game build).

**Encryption seed source:** seeds are now loaded from `Resources/ZeroSecrets.asset` (created by user). Editor builds fall back to hardcoded template defaults with a loud warning. Player builds throw if the asset is missing.
