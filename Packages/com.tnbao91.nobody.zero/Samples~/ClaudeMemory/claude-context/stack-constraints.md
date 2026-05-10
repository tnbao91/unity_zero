# Stack constraints (LOCKED)

The Zero template's stack is deliberately picked. When extending, **do NOT suggest substitutes** — and don't accept "let's switch to X because it's more popular" without surfacing this doc.

## Locked picks + rationale

### DI: **Reflex** (NOT Zenject, VContainer)
- Source-only (no codegen, no DLL). Faster startup than Zenject.
- Minimal API — `RegisterType`, `RegisterFactory`, `RegisterValue`. Easier to reason about than Zenject's 40+ binding methods.
- Resolution model is per-scope (Root + Scene), no Project Context vs Scene Context confusion.

### Async: **UniTask** (NOT `Task<T>` for game code)
- Zero allocation on the hot path (pooled state machines).
- PlayerLoop-aware (`await UniTask.Yield(PlayerLoopTiming.Update)` etc).
- `.AttachExternalCancellation`, `.WithCancellation`, `.SuppressCancellationThrow` — battle-tested for Unity.
- `Task<T>` is fine for non-game library code (analytics SDK adapter etc.) but never for game logic.

### Reactive: **R3** (NOT UniRx)
- Successor to UniRx by Cysharp. Better struct-based observables, lower alloc.
- `Observable<T>` instead of `IObservable<T>`. `Subject<T>` instead of UniRx's Subject.
- BCL transitive deps (Microsoft.Bcl.AsyncInterfaces etc.) — get via NuGetForUnity in this template's setup.

### Tweening: **LitMotion** (NOT DOTween, PrimeTween)
- Burst + Jobs backed. Fastest mobile tween.
- Allocation-free per-tween. DOTween allocates per tween in default mode.
- Composable with UniTask via `ToUniTask()`.

### JSON: **`com.unity.nuget.newtonsoft-json`** (NOT JsonUtility for non-trivial)
- Unity-shipped, no NuGetForUnity needed for JSON.
- Full Newtonsoft API (JObject, JsonConvert, custom converters). JsonUtility lacks dictionary, polymorphism, custom converters.
- ZeroSecrets save service uses it for the JSON envelope.

### String: **ZString** (NOT `string.Format`, raw `+` for hot paths)
- Zero allocation interpolation (`ZString.Format("level {0} score {1}", level, score)`).
- Use for HUD updates, log lines fired per-frame.
- Plain `string.Format` is fine for one-shot dialogs — don't over-apply.

### Localization: **`com.unity.localization`**
- Unity-shipped. Smart Strings, persistence, Editor authoring built-in.
- `IL10nService.Get(key)` wraps `LocalizationSettings.StringDatabase.GetLocalizedString`.
- Don't roll a custom string table.

### Notifications: **`com.unity.mobile.notifications`** (Unified API)
- Unity-shipped. Cross-platform (iOS + Android).
- `INotificationService` wraps it. Permission requested at "value moment", not bootstrap.
- Don't use Firebase Cloud Messaging directly for local notifications.

### Object pool: **`UnityEngine.Pool.ObjectPool`**
- Built-in since Unity 2021. `IPoolService` wraps it for `GameObject` pooling with Addressables key.
- `collectionCheck: true` in Editor catches double-release.
- Don't roll a `Stack<GameObject>` pool — that's what `ReflexPoolService` was before the Phase 1a refactor; it's been replaced with the built-in.

### Input: **New Input System** (NOT legacy `Input.touchCount` etc.)
- Project's "Active Input Handling" is Input System Package only — legacy Input throws at runtime.
- `IInputService` wraps `InputSystem` + EnhancedTouch with gesture detection.
- For ad-hoc reads outside the service: `Keyboard.current`, `Mouse.current`, `Touchscreen.current`.

### IAP: **`com.unity.purchasing`** (Unity IAP)
- Unity-shipped. Consumer can swap to RevenueCat by replacing the `IIapService` impl.
- `StubReceiptValidator` is the always-valid mock; consumer ships a real validator (server-side preferred).

## When to push back

If the user asks to "switch to Zenject" / "use DOTween for this animation" / "Task<T> is more idiomatic" — **push back**. Cite this file. Surface that the substitution would:
- Break consistency with template internals.
- Introduce a second tool with overlapping responsibility.
- Lose a perf or ergonomic benefit the original choice gives.

If they confirm they really want the substitution after hearing the tradeoff, do it — but only in YOUR game asmdef, never modifying Zero itself.

## What's NOT locked

- HTTP client (UnityWebRequest is fine; Cysharp YetAnotherHttpClient also fine).
- ScriptableObjects vs JSON for game data (your call).
- Animation system (Mecanim, Animancer, custom — your call).
- Specific UI library on top of `IUIService` (raw UGUI, UI Toolkit, custom — your call as long as you respect `UIRoot` contract).
- Third-party SDK choices for monetization/analytics — that's the entire point of Mock + extension recipe pattern.

## TL;DR for AI agents

When writing new code in this project:
- DI: `using Reflex.Core; using Reflex.Attributes;` and `[Inject]`.
- Async: `using Cysharp.Threading.Tasks;` and `UniTask` everywhere.
- Reactive: `using R3;` and `Observable<T>` / `Subject<T>`.
- Tween: `using LitMotion;` and `LMotion.Create(...).BindToX(...)`.
- JSON: `using Newtonsoft.Json.Linq;` for `JObject` etc.
- Strings: `using Cysharp.Text;` for `ZString`.

If you see boilerplate that doesn't fit the above, suspect the existing code is wrong and ask before "fixing" it.
