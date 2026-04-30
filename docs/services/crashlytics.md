# Crashlytics Service

## Overview

`ICrashlyticsService` reports uncaught exceptions and breadcrumb logs to a crash-tracking backend. The template ships `MockCrashlyticsService` — calls go to `Debug.Log` so you see what *would* be reported. `CrashlyticsStep` is the only **critical** bootstrap step in the pipeline (per `PLAN.md` §2.11) — if the real impl fails to initialize, bootstrap aborts so subsequent steps don't paper over a broken telemetry surface.

Real impls wrap Firebase Crashlytics, Bugsnag, Sentry, or AppsFlyer's crash module.

## Public API

```csharp
namespace Zero.Core
{
    public interface ICrashlyticsService
    {
        UniTask InitializeAsync(CancellationToken ct = default);
        void RecordException(Exception exception);
        void Log(string message);
        void SetCustomKey(string key, string value);
        void SetUserId(string userId);
    }
}
```

## Mock behavior

`MockCrashlyticsService` (in `Assets/_Project/Scripts/Runtime/Services/Crashlytics/MockCrashlyticsService.cs`) — every call writes to `Debug.Log`/`LogError` with a `[Crashlytics]` prefix. `InitializeAsync` returns `UniTask.CompletedTask` immediately.

## Extension Points

### Swap to Firebase Crashlytics

1. Install Firebase Crashlytics via [Unity Package Manager](https://firebase.google.com/docs/crashlytics/get-started?platform=unity).
2. Configure `google-services.json` (Android) and `GoogleService-Info.plist` (iOS).
3. Add `Firebase.Crashlytics` to `Zero.Services.Crashlytics.asmdef` references.
4. Implement `FirebaseCrashlyticsService : ICrashlyticsService`:

```csharp
using Firebase.Crashlytics;

public sealed class FirebaseCrashlyticsService : ICrashlyticsService
{
    public UniTask InitializeAsync(CancellationToken ct = default)
    {
        // Firebase auto-initializes via FirebaseApp; no explicit step needed
        // in most setups.
        return UniTask.CompletedTask;
    }

    public void RecordException(Exception exception)
        => Crashlytics.LogException(exception);

    public void Log(string message)
        => Crashlytics.Log(message);

    public void SetCustomKey(string key, string value)
        => Crashlytics.SetCustomKey(key, value);

    public void SetUserId(string userId)
        => Crashlytics.SetUserId(userId);
}
```

5. Edit `CrashlyticsServiceInstaller.cs` line 9 — replace `MockCrashlyticsService` with `FirebaseCrashlyticsService`:

```csharp
builder.RegisterType(typeof(FirebaseCrashlyticsService), new[] { typeof(ICrashlyticsService) }, Lifetime.Singleton, Resolution.Lazy);
```

### Swap to Sentry

[Sentry's Unity SDK](https://docs.sentry.io/platforms/unity/) auto-attaches to Unity's `Application.logMessageReceived`, so the wrapper is mostly a passthrough that calls `SentrySdk.CaptureException`. Same installer-line swap.

### Hook Unity's unhandled exception path

Real impls typically subscribe to `Application.logMessageReceived` and forward `LogType.Exception` to `RecordException` automatically — gameplay code shouldn't have to remember to call it. Add this in `InitializeAsync`:

```csharp
Application.logMessageReceived += (message, stack, type) =>
{
    if (type == LogType.Exception)
        RecordException(new Exception($"{message}\n{stack}"));
};
```

## Examples

```csharp
[Inject] private ICrashlyticsService _crash;

private void OnPurchaseFailed(Exception ex, string productId)
{
    _crash.SetCustomKey("last_product_id", productId);
    _crash.Log("IAP purchase failed");
    _crash.RecordException(ex);
}
```

## Known Limitations

- **Mock writes to Debug.Log only.** No file persistence, no upload — flip to a real impl for any QA build that needs crash records.
- **No automatic init guard.** If `CrashlyticsStep` somehow runs before the SDK is ready (race condition in third-party init), `RecordException` calls early can be lost. Real impls should buffer and flush on first connect.
- **`SetUserId` is plaintext.** GDPR / CCPA: do not pass identifiers without consent. Tie this call to `IConsentService.GdprStatus == Personalized`.

## Design Rationale

- **`CrashlyticsStep` is the only critical step** — telemetry is foundational; if it doesn't init, every subsequent crash is invisible. Other steps are non-critical and degrade to mocks/no-op.
- **Mock writes to Debug.Log** rather than no-op so dev work surfaces "what would be sent". A silent mock is hard to verify.
- **Single `RecordException` overload** rather than separate `RecordHandled` / `RecordFatal` — most SDKs treat the distinction via metadata; keep the interface narrow.
