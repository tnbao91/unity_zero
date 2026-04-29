# Phase 2 Manual Verification Checklist

This document lists manual steps to verify Phase 2 features (Input, Audio, Notification) that cannot be tested headless in CI.

**Platform:** iOS device, Android device, Editor (where applicable).

---

## Input Service Verification

### Tap Detection

**Setup:**
- Open `Assets/_Project/Scenes/Bootstrap.unity` in Editor
- Press Play
- Subscribe a debug log to `IInputService.OnTap`:
  ```csharp
  var input = container.Resolve<IInputService>();
  input.OnTap.Subscribe(pos => Debug.Log($"TAP at {pos}"));
  ```

**iOS/Android Device:**
1. Build and deploy to device
2. Tap the screen (quick touch, ~100ms duration)
3. **Expected:** Tap is logged in Logcat / Xcode console
4. **Verify:** Tap within 200ms window and <20px drag registers; slow or far taps do not

### Swipe Detection

**Setup:** Same as tap, but subscribe to `OnSwipe`:
```csharp
input.OnSwipe.Subscribe(swipe => 
    Debug.Log($"SWIPE dir={swipe.Direction.normalized}, mag={swipe.Magnitude}px, vel={swipe.Velocity}px/s"));
```

**iOS/Android Device:**
1. Swipe horizontally (at least 50px distance) in ~300ms
2. **Expected:** Swipe is logged with direction, magnitude, velocity
3. **Verify:** Swipes <50px or >500ms don't fire; diagonal swipes work

### Pinch Detection

**Setup:** Subscribe to `OnPinch`:
```csharp
input.OnPinch.Subscribe(scale => Debug.Log($"PINCH scale={scale}"));
```

**iOS/Android Device (2-finger only):**
1. Place two fingers on screen, move apart (scale up)
2. **Expected:** OnPinch fires with scale > 1.0
3. Pinch inward (scale down)
4. **Expected:** OnPinch fires with scale < 1.0
5. **Verify:** Single finger doesn't pinch; 3+ fingers ignored

### Escape / Back Button

**Setup:** Subscribe to `OnEscape`:
```csharp
input.OnEscape.Subscribe(_ => Debug.Log("ESCAPE pressed"));
```

**PC (Editor):**
1. Press Esc key
2. **Expected:** "ESCAPE pressed" logged

**Android Device:**
1. Press back button (physical or on-screen)
2. **Expected:** "ESCAPE pressed" logged

---

## Audio Service Verification

### Prerequisites

1. **Create mixer asset** (if not already present):
   - Follow `docs/services/audio.md` "Mixer Creation Steps"
   - Add to Addressables as `audio/main_mixer`

2. **Create test audio clips:**
   - Add MP3/WAV files to `Assets/_Project/Content/Audio/`
   - Register in Addressables:
     - `audio/music/test_music.mp3` → address `audio/music/test_music`
     - `audio/sfx/test_sfx.wav` → address `audio/sfx/test_sfx`

### Music Playback and Crossfade

**Setup (in Bootstrap scene, e.g., a test button):**
```csharp
var audio = container.Resolve<IAudioService>();
await audio.PlayMusicAsync("audio/music/test_music", loop: true);
```

**Editor:**
1. Press Play
2. Click button to play music
3. **Expected:** Music starts at low volume (0f), fades in over 0.3s, then plays
4. Click button again
5. **Expected:** Current music fades out over 0.3s, then new track fades in (or same track restarts)
6. **Verify:** No pop/click artifacts; smooth transition

### SFX One-Shot

**Setup:**
```csharp
await audio.PlaySfxAsync("audio/sfx/test_sfx");
```

**Editor:**
1. Click button to play SFX
2. **Expected:** Short sound plays once, AudioSource auto-returns to pool
3. Click multiple times rapidly
4. **Expected:** Multiple SFX play layered (pooled sources work)

### Bus Volume Persistence

**Setup:**
```csharp
audio.SetBusVolume(AudioBus.Music, 0.5f);
```

**Editor:**
1. Press Play, set Music bus to 0.5
2. Observe music volume drops
3. Stop Play, press Play again
4. **Expected:** Music bus is still 0.5 (loaded from save)

**Device (iOS/Android):**
1. Deploy app, adjust music volume in settings menu (if you add it)
2. Close app completely
3. Relaunch app
4. **Expected:** Music volume persists

---

## Notification Service Verification

### Permissions and Scheduling

**iOS Device:**
1. Build and deploy
2. In your settings/test UI, call `await notifService.RequestPermissionAsync()`
3. **Expected:** iOS permission dialog appears; user grants or denies
4. Close app, relaunch
5. Call `RequestPermissionAsync()` again
6. **Expected:** No dialog (cached); returns same result as before

**Android Device:**
1. Build and deploy (Android auto-grants; no dialog)
2. Call `notifService.Schedule("test-1", "Hello", "Body", TimeSpan.FromSeconds(5))`
3. Put app in background for 5+ seconds
4. **Expected:** Notification appears in system tray with title "Hello", body "Body"
5. **Verify:** Tapping notification returns to app (system behavior)

### Notification Cancellation

**Setup:**
```csharp
await notifService.Schedule("daily", "Daily reward", "...", TimeSpan.FromDays(1));
notifService.Cancel("daily");
```

**Device:**
1. Schedule notification
2. Immediately cancel it
3. **Expected:** Notification does NOT appear after delay

### Multiple Notifications

**iOS/Android:**
1. Schedule 3 notifications with different IDs (`"notif1"`, `"notif2"`, `"notif3"`) at 5s delays
2. Put app in background
3. After 5+ seconds
4. **Expected:** All 3 notifications appear in notification center/tray
5. Call `CancelAll()`
6. **Expected:** Remaining scheduled notifications are cleared

---

## Integration Checklist

After verifying each feature individually:

1. **Bootstrap launches cleanly** — no exceptions, all steps complete
2. **All three services initialize** — check Editor console for "[AUDIO]", "[NOTIF]", and no Input errors
3. **Mock services fall back gracefully** — set `#if ZERO_USE_MOCK_AUDIO` in installer, verify app still launches
4. **No asmdef cycles** — `grep -r "Zero.Audio\|Zero.Input\|Zero.Notification" Assets/_Project/Scripts/Runtime/Core/` returns nothing (circular refs would fail here)

---

## Sign-Off

Complete this checklist and commit with message:
```
docs: Phase 2 manual verification passed (input/audio/notification)

- Tap fires on device (200ms, <20px window)
- Swipe fires (≥50px, <500ms window)
- Pinch fires (two-finger only)
- Esc/back fires
- Audio volume persists across restart
- Music crossfade smooth (no pop)
- SFX fires from pool
- Notification schedules and cancels on iOS/Android
- Permission cached and not re-requested
```
