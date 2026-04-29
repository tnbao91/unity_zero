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

---

## Phase 3 UI Verification

### Loading Screen

**Setup:**
1. Create a temporary `Loading.unity` scene
2. Add a Canvas with Slider (progress bar) and TextMeshProUGUI (step name)
3. Attach `LoadingScreenView` component to an empty GameObject, wire the Slider and Text fields
4. In Bootstrap scene, load `Loading.unity` before bootstrap completes (via SceneService)

**Editor:**
1. Open `Loading.unity` and Press Play (will fail without bootstrap context — expected)
2. Build a minimal test scene that initializes `IBootstrapProgressReporter` manually:
   ```csharp
   var reporter = new BootstrapProgressReporter();
   var view = gameObject.AddComponent<LoadingScreenView>();
   // Manually drive progress
   for (float p = 0; p <= 1; p += 0.05f)
   {
       reporter.SetProgress(p, $"Step {p:P0}");
       await UniTask.Delay(100);
   }
   ```
3. **Expected:** Slider fills from 0 to 1; step name updates each frame
4. **Verify:** No exceptions; UI is responsive

### Safe Area

**iOS Simulator (with notch):**
1. Create a Panel with `SafeAreaFitter` in a test scene
2. Add a colored background Image so the panel is visible
3. Press Play
4. **Expected:** Panel is inset from the notch area (top of screen)
5. Rotate device (Cmd+Right Arrow in simulator)
6. **Expected:** Panel re-adjusts anchors; no jitter

**Android Simulator (hole-punch notch):**
1. Same setup as iOS
2. **Expected:** Panel adjusts to the center-top notch area

**PC/Editor (no notch):**
1. Same setup
2. **Expected:** Panel uses full screen (safe area == full screen)

### LocalizedText

**Setup:**
1. Create a test scene with TextMeshProUGUI
2. Attach `LocalizedText`, set `_key` to `ui.test.message`
3. Ensure a localization table has an entry `ui.test.message` = "Hello"

**Editor:**
1. Press Play
2. **Expected:** Text displays "Hello"
3. In code, call `il10nService.SetLocaleAsync(locale)` to switch locale
4. **Expected:** Text immediately updates (if the new locale has the key)
5. **Verify:** No null refs; fallback to key name if translation missing

### Toast Queue

**Setup (not testable headless — queue timestamps and async delays):**
1. In a test scene, resolve `IUIService`
2. Add a button that calls:
   ```csharp
   uiService.ShowToast("Toast 1", 2f);
   uiService.ShowToast("Toast 2", 2f);
   uiService.ShowToast("Toast 3", 2f);
   ```

**Editor (with toast prefab in Addressables at `ui/toast/default`):**
1. Click button
2. **Expected:** Toasts appear sequentially, not overlapping (FIFO)
3. Each toast displays for ~2s, then auto-dismisses
4. Next toast appears after previous one finishes

**Without toast prefab:**
1. **Expected:** No exceptions; console warns "Toast prefab key not found"; ShowToast becomes a no-op

### Layer Canvases

**Editor:**
1. Open `Bootstrap.unity`, Press Play
2. In Hierarchy, search for `[Zero.UI]`
3. **Expected:** Four children: `[Zero.UI.Hud]`, `[Zero.UI.Popup]`, `[Zero.UI.Overlay]`, `[Zero.UI.System]`
4. Each is a Canvas with `sortingOrder` 100/200/300/400
5. Stop Play
6. **Expected:** Canvases disappear (DontDestroyOnLoad is conditional on `Application.isPlaying`)

---

## Sign-Off

Complete Phase 2 + Phase 3 checklist and commit with message:
```
docs: Phase 2–3 manual verification passed (input/audio/notification/ui)

**Phase 2:**
- Tap fires on device (200ms, <20px window)
- Swipe fires (≥50px, <500ms window)
- Pinch fires (two-finger only)
- Esc/back fires
- Audio volume persists across restart
- Music crossfade smooth (no pop)
- SFX fires from pool
- Notification schedules and cancels on iOS/Android
- Permission cached and not re-requested

**Phase 3:**
- Loading screen progress bar fills 0→1 during bootstrap
- Safe area respected on iOS (notch) + Android (hole-punch)
- LocalizedText updates text on locale change
- Toast queue shows toasts sequentially (FIFO)
- Layer canvases spawn at UIStep, persist via DontDestroyOnLoad
```
