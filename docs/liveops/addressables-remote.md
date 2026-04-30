# Addressables Remote Catalog

## Overview

The template ships Addressables wired for **local-only** asset loading — every group's `Build & Load Path` is set to `LocalBuildPath` / `LocalLoadPath`. To ship live-ops content updates without a binary release, point a subset of groups at a CDN. This doc covers the remote-catalog setup and the URL convention; the template intentionally does not hardcode any CDN URL — that choice is per-game.

## Public API

There is no special template API for remote loading. All loads still go through `IAssetService.LoadAsync<T>(key, ct)` ([docs/services/save.md not relevant — see asset service in code]). The Addressables system handles local-vs-remote routing under the hood based on the path each group is configured against.

## Setup

### 1. Pick the CDN

Any HTTPS host that serves files unchanged: AWS CloudFront, Cloudflare R2, Firebase Hosting, GitHub Pages, your own. The CDN must:

- Serve the catalog `.json` and `.bin` files with `Content-Type: application/octet-stream` (or `application/json`) — incorrect MIME on iOS sometimes causes downloads to fail.
- Allow CORS if you ever load Addressables from WebGL.
- Honor cache-control with reasonable TTL — Addressables uses content-hashed filenames so long TTLs are safe.

### 2. Configure RemoteLoadPath

In Unity, open `Window → Asset Management → Addressables → Profiles`. Edit the `Default` profile (or create a new one for production):

| Variable | Value |
|---|---|
| `RemoteBuildPath` | `ServerData/[BuildTarget]` |
| `RemoteLoadPath` | `https://YOUR-CDN/addressables/[BuildTarget]` |
| `LocalBuildPath` | `Library/com.unity.addressables/aa/[BuildTarget]` |
| `LocalLoadPath` | `{UnityEngine.AddressableAssets.Addressables.RuntimePath}/[BuildTarget]` |

Substitute `YOUR-CDN` with your domain. The `[BuildTarget]` placeholder is filled in by Addressables at build time (`StandaloneOSX`, `iOS`, `Android`, `WebGL`, etc.).

### 3. Mark groups as remote

For each `AddressableAssetGroup` you want to ship via CDN:
1. Inspect the group asset.
2. Under `Content Packing & Loading → Build & Load Paths`, switch from `Local` to `Remote`.
3. Under `Advanced Options`, ensure `Use Asset Bundle Cache` is on (default).

Groups that should remain in the binary (e.g. core scenes loaded before remote catalog is fetched) stay on `Local`.

### 4. Enable remote catalog

In `Addressables Settings`:
- `Build Remote Catalog` = on.
- `Build & Load Paths` = same Remote / Local pair as above.

### 5. Build + upload

1. `Window → Asset Management → Addressables → Groups → Build → New Build → Default Build Script`. Output lands in `ServerData/[BuildTarget]`.
2. Upload the entire `ServerData/[BuildTarget]/` folder to your CDN at the path matching `RemoteLoadPath`.
3. In CI, this becomes `aws s3 sync ServerData/iOS s3://your-bucket/addressables/iOS --acl public-read` or equivalent.

### 6. Content updates

Subsequent live-ops drops:
1. Make the asset / scene / scriptable-object changes in the project.
2. `Addressables → Groups → Build → Update a Previous Build` and pick the `addressables_content_state.bin` from the binary build that's currently shipping.
3. Upload the resulting delta.
4. Players pick up the new catalog on next launch (or on next `IAssetService.LoadAsync` if catalog refresh is forced).

## Extension Points

**Forcing a catalog refresh.** Default behavior fetches the catalog once per session. To force re-fetch (e.g. after `AppPaused(false)` indicating long background time):

```csharp
await Addressables.UpdateCatalogs(autoCleanBundleCache: true).ToUniTask();
```

**CDN signing / auth.** If your CDN requires signed URLs, intercept `RemoteLoadPath` resolution by replacing the `IResourceProvider` chain. This is rare — most live-ops setups use public-read with content-hashed filenames.

**Region routing.** Some games need region-specific CDNs (China vs ROW). Set `RemoteLoadPath` from `IRemoteConfigService.GetString("addressables.cdn_url")` at runtime — but the catch is that Addressables resolves the path *very early* in app startup, before bootstrap RemoteConfigStep has fetched. Workaround: ship a small "router" build with the catalog URL hardcoded per region.

## Examples

`AssetStep` (already in the template) initializes Addressables — once `RemoteLoadPath` is configured, the step transparently fetches the remote catalog before any subsequent `IAssetService.LoadAsync` call.

```csharp
// Inside your gameplay code — no special remote API needed.
var handle = await _assetService.LoadAsync<GameObject>("levels/world1/level5_remote", ct);
var instance = Object.Instantiate(handle.Asset);
// ... later:
handle.Dispose();
```

Verify a remote group is wired by enabling Addressables logging:
```
Edit → Project Settings → Addressables → Send Profiler Events = true
```
Then in the Editor profiler you'll see "Remote" tagged loads.

## Known Limitations

- **No CDN ships with the template.** Consumer responsibility entirely.
- **No catalog version pinning.** Once you upload a new catalog, all clients on the next session use it. Roll back by re-uploading the prior catalog snapshot.
- **WebGL caching is browser-controlled.** IndexedDB cache, not Unity's bundle cache; behavior differs from native.
- **iOS App Store rules.** Apple permits Addressables content updates that don't change app behavior in spirit (no new monetization vectors via remote scripts). Stay within data-only updates.
- **Signed-URL setups are non-trivial** in Addressables. If your CDN auth changes per request, expect to write a custom resource provider.

## Design Rationale

- **Template ships local-only** because every game's CDN choice differs and a hardcoded URL would either be wrong or imply a service the template doesn't provide. The opportunity cost of "preconfigured remote" is "wrong configuration that breaks on day one".
- **No template-side abstraction over Addressables remote** — Unity's pathing is sufficient and a wrapper would just hide useful settings (cache strategy, retry, catalog versioning). Document the recipe; let consumers configure.
- **`RemoteLoadPath` uses `[BuildTarget]` substitution** so a single profile serves iOS / Android / WebGL without per-platform variants.
