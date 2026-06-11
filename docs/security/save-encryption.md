# Save Encryption

## Overview

The `EncryptedJsonSaveService` protects user progress from casual tampering using **AES-256-CBC + HMAC-SHA256**. The encrypted file format is `[HMAC 32B][IV 16B][AES-CBC ciphertext]` wrapping a JSON envelope `{version, data}`. Encryption keys are derived from seeds stored in `Resources/ZeroSecrets.asset` (per-game, gitignored).

## Threat Model

**What this protects:** casual editing (hex-editing save files to modify currency, unlocked levels, etc.). The HMAC-SHA256 ensures any byte flip is detected, causing the service to reset the save to empty on load failure.

**What this does NOT protect:**
- **Reverse engineering:** the template is open-source. A determined player can read the code, extract default seeds from the build, and decrypt their own saves.
- **Replay attacks:** old save files encrypted with the same keys can be restored without server validation.
- **Cheating progression:** if progression is tied to real-world money or leaderboards, **server validation is mandatory**. Client-side crypto is not sufficient. Example: verify on the backend that the claimed level unlock was earned through legitimate gameplay (check timestamps, event logs, etc.).

**Hybrid casual context:** most hybrid casual games tolerate casual cheating (single-player, offline-first progression). If your game has in-game purchases, ads, or multiplayer, implement server-side validation for critical progression milestones.

## Key Derivation and Storage

**How seeds are generated:**
1. Consumer copies `Assets/Resources/ZeroSecrets.asset.example` → `Assets/Resources/ZeroSecrets.asset`.
2. Opens the asset in the Inspector and replaces both `_aesSeed` and `_hmacSeed` string fields with unique, per-game values (e.g., "MyGameTitle.v1.Aes.2024" and "MyGameTitle.v1.Hmac.2024").
3. At startup, `EncryptedJsonSaveService` loads the asset and derives 32-byte keys via SHA256:
   ```
   AES_Key = SHA256(UTF8("MyGameTitle.v1.Aes.2024"))
   HMAC_Key = SHA256(UTF8("MyGameTitle.v1.Hmac.2024"))
   ```

**Why string seeds?** Easy to inspect and modify in the Inspector without hex-editing binary blob. The seeds themselves are not secrets (they can be reversed from the compiled code); what matters is that each game has unique seeds so saves from Game A don't accidentally decrypt Game B's format.

**Distribution:** `ZeroSecrets.asset` is gitignored. Each game build includes its own asset:
- Development builds include the dev asset (secrets may be committed locally, never to the main repo).
- Release builds include the release asset (secrets generated per-game, never shared).

## Encryption Algorithm

**AES-256-CBC:** symmetric encryption. The 256-bit key is derived as above. Each encryption generates a random 16-byte IV (initialization vector) which is prepended to the ciphertext.

**HMAC-SHA256:** message authentication code. Computed over `IV + ciphertext` to detect tampering. If the HMAC doesn't match on decryption, the file is considered corrupted (not a cryptographic failure, just stale/tampered).

**Layout:**
```
[HMAC (32 bytes)] [IV (16 bytes)] [AES-CBC ciphertext (variable)]
|  Authentication  |  Randomization |  Encrypted JSON envelope  |
```

On load failure (HMAC mismatch, ciphertext invalid), the service **does not throw**; instead, it logs the error and resets `_data` to empty. This is a recoverable state for single-slot games (player loses progress, but can restart cleanly).

## Key Rotation vs Schema Migration

These are two different things and the template handles them differently.

**Schema migration** changes the *shape* of the JSON `data` payload (rename a key, split a value into two, drop an obsolete field). Bump `CurrentVersion` in `EncryptedJsonSaveService` and add a branch in the `Migrate(JObject, from, to)` method:

```csharp
private static JObject Migrate(JObject data, int from, int to)
{
    if (from == 0 && to >= 1)
    {
        if (data["player_level"] != null)
        {
            data["level"] = data["player_level"];
            data.Remove("player_level");
        }
    }
    return data;
}
```

`Migrate` runs *after* successful decryption, so it does not help with key changes.

**Key rotation** changes the AES/HMAC seeds. There is no in-place rotation in the v1 template: an old save encrypted with old seeds will fail HMAC under new seeds and reset to empty (consumer loses progress). For a live-ops rotation you need to either (a) keep both seed sets in the build, decrypt-with-old → re-encrypt-with-new on first launch, or (b) push a migration build that ships before the rotation. Both require code beyond the v1 surface; design the rotation strategy before shipping.

## Best Practices

1. **Never hardcode seeds in the shipped binary.** Always load from `ZeroSecrets.asset`.
2. **Never commit `ZeroSecrets.asset` to the main repo.** Use `.gitignore` and document the setup in your Quick Start.
3. **For multi-profile saves:** `EncryptedJsonSaveService` is `sealed` and writes a single `save.dat`. To support multiple slots, write a sibling impl of `ISaveService` (in `Zero.Services.Save` or your own asmdef) that owns multiple files keyed by slot id, then rebind `ISaveService` in `SaveServiceInstaller`.
4. **For cloud save:** after saving locally, also send the encrypted bytes to a server. Don't decrypt on the client to re-transmit. Store encrypted blob server-side and re-download on new devices.
5. **Server validation:** if economy or leaderboards matter, always validate client-reported progression server-side. Log events (level started, level completed, currency earned) and verify they match legitimate play patterns.
6. **IL2CPP stripping:** saved types are deserialized via Newtonsoft reflection (`token.ToObject<T>()`), which the IL2CPP stripper cannot see — preserve your save-model types in a `link.xml` or they fail to deserialize **on device only**. Template + details: `Samples~/BootstrapScene/link.xml` and PITFALLS → "IL2CPP strips save-model types".
7. **Corrupt-file quarantine:** decrypt/parse failures move the file to `save.dat.corrupt` before resetting. Wire your support flow to upload that file — it is the only recovery artifact a player has.

## Design Rationale

**Why AES-256-CBC + HMAC instead of AES-GCM?** GCM is more modern and combines encryption + authentication in one primitive. However, it's not natively available in the .NET Standard 2.0 APIs Unity uses; you'd need a third-party library. CBC + HMAC is supported by built-in `System.Security.Cryptography`, is well-understood, and is defended in the docs.

**Why not encrypt with a per-player key?** That would require a server to issue per-player encryption keys and store the plaintext progress to validate against. For offline-first hybrid casual, a per-game key is simpler and sufficient.

**Why JSON envelope with version?** Plain JSON is human-readable during debugging (when unencrypted in editor logs). The version field future-proofs against format changes without requiring a whole new save file format.

**Why reset on HMAC fail, not throw?** In single-slot hybrid casual, a hard fail blocks the game entirely; resetting to empty is a recoverable fallback. For monetised economies the right move is to swap the implementation behind `ISaveService` (in `SaveServiceInstaller`) for one that fails loudly and gates launch on a server-side recovery flow — there is no `IFailLoudHandler` extension point in v1, only the option to replace the binding.
