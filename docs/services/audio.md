# Audio Service

## Overview

Wraps `UnityEngine.Audio.AudioMixer` (asset loaded via Addressables) for centralized bus routing and volume control. Provides persistent music playback with crossfade, one-shot SFX via pooling, and per-bus volume persistence (ISaveService). Falls back gracefully if mixer asset is missing (template default).

**Key design:** No mixer asset shipped. Manual creation required (5 buses, 5 exposed parameters). Document below explains setup.

## Public API

```csharp
public interface IAudioService
{
    UniTask InitializeAsync(CancellationToken ct = default);
    UniTask PlayMusicAsync(string clipKey, bool loop = true, CancellationToken ct = default);
    void StopMusic();
    UniTask PlaySfxAsync(string clipKey, CancellationToken ct = default);
    void SetBusVolume(AudioBus bus, float volume);    // 0..1, persisted
    float GetBusVolume(AudioBus bus);
}

public enum AudioBus
{
    Master, Music, Sfx, Ui, Voice
}
```

**Addressables keys:**
- Mixer asset: `audio/main_mixer` (AudioMixer)
- Music clips: e.g. `audio/music/intro`, `audio/music/level1` (AudioClip)
- SFX clips: e.g. `audio/sfx/button_click`, `audio/sfx/level_complete` (AudioClip)

**Persistence keys (ISaveService):**
- `audio.bus.master`, `audio.bus.music`, `audio.bus.sfx`, `audio.bus.ui`, `audio.bus.voice` (float 0..1)

**Implementation:** `AudioMixerService` (real, requires mixer asset) vs `MockAudioService` (mock, in-memory volumes). Swap via `#if ZERO_USE_MOCK_AUDIO` in `AudioServiceInstaller.cs`.

## Extension Points

1. **Custom mixer setup:** Replace `audio/main_mixer` Addressable with your own mixer. Ensure it has 5 groups (Master, Music, Sfx, Ui, Voice) and 5 exposed parameters (BusMaster, BusMusic, BusSfx, BusUi, BusVoice). Change the key string in `AudioMixerService.InitializeAsync()`.

2. **Per-bus SFX:** Currently all SFX route to one pool. To separate SFX streams (e.g., environment SFX vs dialogue), create additional pools in the service and add new routing logic.

3. **Volume ramps:** `PlayMusicAsync` uses 0.3s crossfade via LitMotion. Adjust the duration (currently hardcoded) in the method.

4. **Spatial audio:** AudioSource components created by the pool support 3D positioning. `AudioMixerService` is `sealed`; add position/spatialBlend support by swapping the binding in `AudioServiceInstaller` for a custom `IAudioService` implementation (or a decorator wrapping `AudioMixerService`).

5. **Platform support:** Audio Service requires the mixer asset on all platforms and no-ops gracefully on unsupported targets.

## Examples

**Playing music with auto-load and crossfade:**
```csharp
var service = container.Resolve<IAudioService>();
await service.PlayMusicAsync("audio/music/level1", loop: true);
```

**Playing one-shot SFX:**
```csharp
await service.PlaySfxAsync("audio/sfx/button_click");
```

**Adjusting bus volume (persists):**
```csharp
service.SetBusVolume(AudioBus.Music, 0.5f);
var current = service.GetBusVolume(AudioBus.Music);  // 0.5f
```

**Saving and loading (automatic):**
Bus volumes are automatically saved via `ISaveService.Set()` on each `SetBusVolume()` call. On next app launch, `InitializeAsync()` loads saved values. No explicit save call needed.

## Known Limitations

1. **No mixer asset in template:** `audio/main_mixer` must be created manually in your project. See "Mixer Creation Steps" below. A fresh clone will log a warning and fall back to per-source volume (no bus routing).

2. **Music source is global, single:** Only one music track plays at a time. Crossfading between two simultaneous tracks requires two AudioSource components and custom blending logic.

3. **Music crossfade has a load gap:** `PlayMusicAsync` fades out the current track, then awaits the next clip's Addressable load, then fades in. There is a brief silent gap during clip load (typically <100ms but depends on storage). True overlapping crossfade requires a second persistent `AudioSource` and is deferred to v2.

4. **SFX pool has fixed size:** Pool template is created once at init; cannot dynamically resize. If you need hundreds of simultaneous SFX, increase pool size or manage your own pool.

5. **No 3D falloff by default:** SFX sources spawn at world origin (no spatial). To enable 3D, modify the SFX pool template's AudioSource (set Spatial Blend > 0) and pass position to `PlaySfxAsync()`.

## Design Rationale

**Why Addressables for mixer asset?**  
Mixer is per-game config (different games have different bus layouts). Addressables allow consumers to swap `audio/main_mixer` without code change. Defensive warning if missing allows fresh clones to still launch (mock fallback).

**Why explicit `InitializeAsync`?**  
Mixer load is async (Addressables). Service cannot initialize in constructor. `InitializeAsync` is called by `AudioStep` during bootstrap, guaranteeing mixer is ready before any audio play.

**Why LitMotion for music crossfade?**  
LitMotion is the project's tweening standard. Volume tween bindings are built-in (`BindToVolume`). Avoids extra DOTween dependency.

**Why bus volumes in ISaveService, not Unity Preferences?**  
Unified persistence layer. All game state (including audio preferences) flows through `ISaveService`, which handles encryption, serialization, and migrations. Keeps concerns separated.

## Mixer Creation Steps

If `audio/main_mixer` is missing:

1. **Create mixer asset:**
   - Right-click in Project → Audio Mixer
   - Rename to `MainMixer`
   - Place in `Assets/_Project/Content/Audio/` (create folder if needed)

2. **Set up 5 bus groups:**
   - Open the mixer in the inspector
   - In "Hierarchy" panel, add 5 child groups under Master:
     - Name: Music
     - Name: Sfx
     - Name: Ui
     - Name: Voice
     - (Master is default root)

3. **Expose 5 volume parameters:**
   - Right-click each group → Edit with Full Inspector
   - Under Attenuation (Volume) slider, click the expose icon (circle icon to the right)
   - Rename the exposed parameter:
     - Master → `BusMaster`
     - Music → `BusMusic`
     - Sfx → `BusSfx`
     - Ui → `BusUi`
     - Voice → `BusVoice`

4. **Add to Addressables:**
   - Select MainMixer in Project
   - In Inspector, check "Include in Build" (Addressables)
   - Set address to `audio/main_mixer`

5. **Verify in code:**
   - Open Addressables Groups window (`Window → Asset Management → Addressables → Groups`)
   - Search for `audio/main_mixer` — should appear in a group

On next `InitializeAsync()`, the mixer will load successfully and bus routing will work.

## Testing

**EditMode:** `AudioBusPersistenceTests` verifies volume persistence via stub ISaveService (no real mixer needed).

**Manual checklist:** See `docs/testing/manual-checklist.md`.
