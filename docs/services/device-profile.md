# Device Profile Service

## Overview

`IDeviceProfileService` classifies the running device into a `DeviceTier` (`Low` / `Mid` / `High`) and applies a matching set of quality settings (target FPS, texture mip limit, shadows, MSAA). The shipped impl is `DeviceProfileService` (`Packages/com.tnbao91.nobody.zero/Runtime/Services/DeviceProfile/DeviceProfileService.cs`) — a **real** service using `SystemInfo` for detection and `QualitySettings` / `Application.targetFrameRate` to apply.

Hybrid-casual games run on a huge range of hardware; this service gives one tier decision the whole game can read (`Current`) instead of scattering `SystemInfo.systemMemorySize` checks across gameplay, VFX, and UI.

## Public API

```csharp
namespace Zero.Core
{
    public enum DeviceTier { Low, Mid, High }

    public interface IDeviceProfile
    {
        DeviceTier Tier { get; }
        int TargetFps { get; }
        int TextureLimit { get; }       // QualitySettings.globalTextureMipmapLimit
        bool ShadowsEnabled { get; }
        int MsaaSampleCount { get; }    // QualitySettings.antiAliasing
        bool PostProcessingEnabled { get; }
    }

    public interface IDeviceProfileService
    {
        IDeviceProfile Current { get; }
        DeviceTier DetectedTier { get; }
        void Apply();
        void Override(DeviceTier tier);
    }
}
```

| Member | Behavior |
|---|---|
| `DetectedTier` | Hardware-detected tier, computed once in the constructor. Never changes. |
| `Current` | The active `IDeviceProfile` (settings bundle). Equals the detected tier's profile until `Override` is called. |
| `Apply()` | Pushes `Current` into Unity: `globalTextureMipmapLimit`, `Application.targetFrameRate`, `QualitySettings.shadows` (`HardOnly`/`Disable`), `QualitySettings.antiAliasing`. |
| `Override(tier)` | Forces `Current` to a chosen tier's profile (e.g. from a settings menu). Call `Apply()` afterward to take effect. |

Default tier table (`DefaultDeviceProfile`): RAM `< 3 GB` → Low, `< 6 GB` → Mid, else High.

| Tier | FPS | TextureLimit | Shadows | MSAA | PostFX |
|---|---|---|---|---|---|
| Low | 30 | 1 | off | 0 | off |
| Mid | 60 | 0 | on | 2 | on |
| High | 60 | 0 | on | 4 | on |

> `PostProcessingEnabled` is exposed on the profile for the consumer's render setup to read; the service does **not** toggle the URP post-processing stack itself.

## Extension Points

The impls are `sealed`. Two seams:

1. **Swap the service** in `DeviceProfileServiceInstaller.cs` to change detection (e.g. a GPU-model lookup table instead of RAM thresholds):

   ```csharp
   builder.RegisterType(
       typeof(GpuLookupDeviceProfileService),
       new[] { typeof(IDeviceProfileService) },
       Lifetime.Singleton,
       Resolution.Lazy);
   ```

2. **Provide custom per-tier profiles** by implementing `IDeviceProfile` (instead of `DefaultDeviceProfile`) in your replacement service — e.g. a 120 FPS High tier for ProMotion devices, or extra tiers folded into the three enum values.

## Examples

Detect, apply at boot, then honor a player override from settings:

```csharp
_deviceProfile.Apply();                       // apply detected tier at startup

// Player picks "Battery saver" in settings:
_deviceProfile.Override(DeviceTier.Low);
_deviceProfile.Apply();
```

Gate an expensive effect on tier:

```csharp
if (_deviceProfile.Current.Tier >= DeviceTier.Mid && _deviceProfile.Current.PostProcessingEnabled)
    EnableBloom();
```

## Known Limitations

- **RAM-only heuristic by default.** `systemMemorySize` is a crude proxy — a high-RAM low-GPU device lands in High. Swap in a GPU/SoC lookup for shipping titles that care.
- **`Apply()` is manual.** The service never applies on its own; the consumer (typically a bootstrap step or settings screen) calls `Apply()`. `Override` without `Apply()` changes `Current` but not the live `QualitySettings`.
- **Shadows are `HardOnly` or `Disable`.** No soft-shadow tier; extend the profile/apply if you need finer control.
- **Detection runs once.** Thermal throttling / runtime downgrade is not modeled — re-`Override` from your own thermal monitor if needed.

## Design Rationale

- **One tier decision, read everywhere.** A single `Current` profile keeps quality logic consistent and testable instead of re-deriving from `SystemInfo` in multiple systems.
- **Detect ≠ apply.** Separating `DetectedTier` (immutable fact) from `Current` (mutable choice) and `Apply()` (the side effect) lets a settings menu override and re-apply cleanly without re-detecting.
- **`PostProcessingEnabled` is advisory.** The template doesn't own the consumer's URP renderer asset, so the profile reports the recommendation and lets the render setup act on it.
