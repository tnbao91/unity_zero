# Notification Service

## Overview

Wraps `Unity.Notifications` package (v2.4.3+) for cross-platform local notifications. iOS uses `iOSNotificationCenter`, Android uses `AndroidNotificationCenter`. Requests user permission on-demand (not at bootstrap), caches permission outcome in `ISaveService` to avoid re-prompting. Editor and unsupported platforms silently no-op.

**Key design:** Permission request is *not* automatic. Consumer controls when to prompt (typically after first level or in settings screen) — see "Design Rationale."

## Public API

```csharp
public interface INotificationService
{
    UniTask InitializeAsync(CancellationToken ct = default);
    UniTask<bool> RequestPermissionAsync(CancellationToken ct = default);
    void Schedule(string id, string title, string body, TimeSpan delay);
    void Cancel(string id);
    void CancelAll();
}
```

**Persistence key (ISaveService):**
- `notification.permission.requested` (bool) — caches whether permission was requested; returned on subsequent calls

**Implementation:** `UnityMobileNotificationService` (real, platform-specific) vs `MockNotificationService` (mock, logs only). Swap via `#if ZERO_USE_MOCK_NOTIFICATION` in `NotificationServiceInstaller.cs`.

## Extension Points

1. **Custom permission UI:** Override `RequestPermissionAsync()` to show your own popup before calling platform permission dialog. Wrap the result in your async flow.

2. **Rich notifications (Android):** Extend `Schedule()` to accept `AndroidNotification` config (bigText, smallIcon, color, priority). Currently all use defaults.

3. **Notification categories (iOS):** Pass `UNNotificationCategory` to `iOSNotificationCenter` in `InitializeAsync()` to enable notification actions (reply, dismiss, custom buttons). Currently notifications are simple alerts.

4. **Scheduled delivery tracking:** Add a `Dictionary<string, (scheduled time, clip key)>` cache to track active notifications. Call `OnNotificationReceived` observer to update game state when user taps a notification.

## Examples

**Request permission at a value moment (after first level):**
```csharp
// In your level-complete state handler:
var notifService = container.Resolve<INotificationService>();
bool granted = await notifService.RequestPermissionAsync();
if (granted) {
    Debug.Log("Notifications enabled!");
}
```

**Schedule a daily reminder:**
```csharp
notifService.Schedule(
    id: "daily-login",
    title: "Come back!",
    body: "Your rewards are waiting.",
    delay: TimeSpan.FromHours(24)
);
```

**Cancel all when resetting game:**
```csharp
notifService.CancelAll();
```

## Known Limitations

1. **No callback on notification tap:** Current API schedules and cancels only. To handle user tapping a notification in-game, subscribe to platform-specific events (e.g., `iOSNotificationCenter.OnNotificationReceived`, `AndroidNotificationCenter.onNotificationReceivedCallback`). Consumer responsibility.

2. **Simple text only:** Rich payloads (images, custom layouts) require per-platform setup. Possible but out of scope for v1.

3. **Editor no-op:** Notifications don't actually fire in the editor. Test on device or use `MockNotificationService` for headless CI.

4. **iOS requires developer signing:** Push notifications (scheduled local) work unsigned; rich features may require App ID provisioning.

5. **Android requires Google Play Store:** Some notification features (big picture, big text) work on test devices; full feature set requires Google Play certification.

## Design Rationale

**Why permission is NOT requested at bootstrap?**  
Hybrid casual games maximize permission grant rate by asking *at a value moment* (e.g., after first level, in daily-login popup). Asking at app launch (before user invests) has ~10x lower acceptance. Template does not enforce this anti-pattern; consumers decide when to call `RequestPermissionAsync()` based on their funnel.

**Why `NotificationStep.OnExecuteAsync()` skips `RequestPermissionAsync()`?**  
`NotificationStep` only calls `InitializeAsync()`. Permission request moved to consumer code (see Example above). This keeps bootstrap concerns separate from monetization funnel decisions.

**Why cache permission in ISaveService?**  
Avoid re-prompting user if they've already declined. OS-level permission state is persistent; ISaveService mirrors it so we don't re-request the same grant/deny twice in one app session. Second call to `RequestPermissionAsync()` returns cached bool without showing dialog.

**Why separate iOS and Android implementation?**  
Local notification APIs diverge significantly (`iOSNotificationCenter` vs `AndroidNotificationCenter`; different permission models, channel registration, etc.). Conditional compilation (`#if UNITY_IOS / UNITY_ANDROID`) is cleaner than runtime abstraction. Consumer code doesn't see the ifdef — transparent at `INotificationService` boundary.

**Why `Schedule()` takes `TimeSpan delay` not `DateTime fireTime`?**  
Simplifies common case: "remind me in 24 hours." Relative delays are more intuitive than absolute times. If consumers need absolute scheduling, they can compute `delay = absoluteTime - DateTime.Now`.

## Testing

**EditMode:** `NotificationPersistenceTests` verifies permission caching via stub ISaveService (platform stubs prevent actual notification firing).

**Manual checklist:** See `docs/testing/manual-checklist.md`.

**On-device verification:**
- iOS: Schedule a notification, put app in background. Local notification appears in notification center after delay.
- Android: Likewise. Notification appears in system tray.
