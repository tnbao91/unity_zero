# Pitfalls and Lessons

This file collects concrete pitfalls hit during the Unity Zero v2 implementation. **Read it before extending any of the systems below** — every entry came out of a real bug that wasn't caught until tests or runtime exposed it. Both AI assistants (Claude) and human contributors should treat this as load-bearing context.

---

## EditMode safety (production code must run in EditMode)

Unity's editor APIs reject several runtime calls when invoked from editor scripts or EditMode tests. Production code that ships these calls without guards looks fine in play mode and silently fails the moment it runs in tests or tooling.

### `DontDestroyOnLoad` throws in EditMode

```csharp
// WRONG — throws InvalidOperationException in EditMode tests
Object.DontDestroyOnLoad(go);

// RIGHT
if (Application.isPlaying) Object.DontDestroyOnLoad(go);
```

See `Assets/_Project/Scripts/Runtime/Services/Pool/UnityPoolService.cs` `EnsureRoot()`.

### `Object.Destroy` is play-mode-only

```csharp
// WRONG — throws "Destroy may not be called from edit mode"
Object.Destroy(go);

// RIGHT — pick the correct call by mode
if (Application.isPlaying) Object.Destroy(go);
else Object.DestroyImmediate(go);
```

`UnityPoolService.SafeDestroy(GameObject)` centralizes this — reuse it when adding new disposal paths instead of inlining `Object.Destroy`.

### `UniTask.Yield()` does not tick in EditMode

`UniTask.Yield()` defaults to `PlayerLoopTiming.Update`, which only fires inside Unity's runtime player loop. In EditMode tests (or any editor script), the await never resumes cleanly and the surrounding loop aborts after the first iteration.

```csharp
// WRONG — silently breaks the loop in EditMode
for (int i = 0; i < count; i++)
{
    DoWork(i);
    if (i % 8 == 0) await UniTask.Yield(ct);
}

// RIGHT — skip the breathe in EditMode
if (Application.isPlaying && i % 8 == 0)
    await UniTask.Yield(ct);
```

If you genuinely need an EditMode-compatible yield, look into `EditorApplication.update`-based schedulers; the simpler answer is usually "don't yield in EditMode."

### `UnityEngine.Pool.ObjectPool` prewarm idiom

A naive prewarm loop only ever produces one instance:

```csharp
// WRONG — every Release pushes onto the same Stack;
// the next Get pops the same instance back. CountInactive ends at 1.
for (int i = 0; i < count; i++)
{
    var x = pool.Get();
    pool.Release(x);
}
```

Real prewarm has to hold N instances out simultaneously, *then* release them in one go so `createFunc` is forced to run N times:

```csharp
var held = new GameObject[count];
for (int i = 0; i < count; i++) held[i] = pool.Get();
for (int i = 0; i < count; i++) pool.Release(held[i]);
```

`UnityPoolService.GameObjectPool.Prewarm(int)` does this. Don't change it back.

---

## Asmdef plumbing

`autoReferenced: false` is set on every Zero asmdef so dependencies stay explicit. The cost is that adding a dep requires editing both consumer and (when adding a new service) `Zero.Bootstrap.asmdef`. Three traps:

### Transitive types leaking through wrapped Unity packages

When wrapping a Unity package, the wrapper's public/internal API surface may expose types from a *different* assembly. The asmdef must list both.

```
Zero.Services.Localization
  must reference Unity.Localization      (the wrapped API)
  must reference Unity.ResourceManager   (LocalizationSettings.InitializationOperation
                                          returns AsyncOperationHandle<>)
  must reference UniTask.Addressables    (.ToUniTask() extension on the handle)
```

Check the actual return / parameter types of every Unity API you call. If the type lives in `UnityEngine.X.dll`, you reference `UnityEngine.X` (or whatever the asmdef name is — `Unity.ResourceManager`, not `UnityEngine.ResourceManagement`).

### NuGetForUnity defaults `Editor.enabled: 0` on plugin metas

That excludes the DLL from any asmdef with `includePlatforms: ["Editor"]` — Editor-only test asmdefs in particular. Symptoms: `using R3;` fails with CS0246, or `Subscribe(Action<T>)` is invisible (only the raw `Subscribe(Observer<T>)` instance method resolves), giving CS1660 on lambda binding.

Fix: edit the `.dll.meta` and flip `Editor.enabled: 1`. The repo carries patched metas for R3 and its transitive deps:

```
Assets/Packages/R3.1.3.0/lib/netstandard2.1/R3.dll.meta
Assets/Packages/Microsoft.Bcl.AsyncInterfaces.6.0.0/.../*.dll.meta
Assets/Packages/Microsoft.Bcl.TimeProvider.8.0.0/.../*.dll.meta
Assets/Packages/System.ComponentModel.Annotations.5.0.0/.../*.dll.meta
Assets/Packages/System.Threading.Channels.8.0.0/.../*.dll.meta
```

Re-running NuGetForUnity "Restore" can revert these. If EditMode tests stop discovering R3 symbols out of nowhere, check those metas first.

### asmdef-shipped vs precompiled-DLL refs

`UniTask` and `Reflex` are OpenUPM packages with their own asmdefs — reference them by asmdef name (`"UniTask"`, `"Reflex"`).

`R3`, `Newtonsoft.Json`, `ZString` are NuGet DLLs in `Assets/Packages/` — **not** asmdefs. With `overrideReferences: false` (the project default) they're auto-included via Unity's plugin importer. With `overrideReferences: true` you must list each `.dll` in `precompiledReferences`.

A `"R3"` entry in an asmdef's `references` array is silently ignored (no R3.asmdef exists) — it's not the cause of R3 working in any of the runtime asmdefs. They get R3 because `overrideReferences: false` plus the patched `.dll.meta`.

### Test Runner discovery

Unity's Test Runner only enumerates EditMode test asmdefs that have `includePlatforms: ["Editor"]`. Removing that line (e.g., to dodge the `Editor.enabled: 0` plugin issue) makes the asmdef compile but disappear from the Test Runner UI. Combine `includePlatforms: ["Editor"]` with patched plugin metas — don't pick one over the other.

### Pre-commit `.cs.meta` for assets that reference scripts

`Assets/Resources/ZeroSecrets.asset.example` references `ZeroSecrets.cs` by GUID. If `ZeroSecrets.cs.meta` isn't tracked in git, Unity generates a fresh random GUID on first import and the example asset's script reference dangles. The repo pre-commits `ZeroSecrets.cs.meta` with a deterministic GUID. Apply the same pattern any time you ship a sample/template `.asset` that binds to a script.

### `Observable<T>.Subscribe(Action<T>)` requires `using R3;` at the call site

`R3.Observable<T>` only declares `Subscribe(Observer<T>)` as a class member. `Subscribe(Action<T> onNext)` is an **extension method** in the `R3` namespace. A test file that uses `bus.On<TEvent>().Subscribe(evt => ...)` without `using R3;` compiles to the `Observer<T>` overload and emits `CS1660: Cannot convert lambda expression to type 'Observer<T>' because it is not a delegate type`. This bit Phase 4 round 2 in two test files. Always add `using R3;` to any file that subscribes via lambda — even if the file doesn't otherwise reference R3 types directly (the extension lookup still needs the namespace in scope).

---

## Reflex DI

### Pick exactly one public constructor

Reflex's `ConstructorInjector` picks the constructor with the most parameters. If overloads diverge in arity, Reflex tries to resolve every parameter from the container — including `string`, which is never bound — and throws `UnknownContractException` at first resolve.

```csharp
// WRONG — Reflex picks the (ILogService, string) ctor and fails to resolve String
public sealed class Foo
{
    public Foo(ILogService log) : this(log, "default") { }
    public Foo(ILogService log, string name) { ... }
}

// RIGHT — single ctor, internal default for the secondary value
public sealed class Foo
{
    private const string DefaultName = "default";
    public Foo(ILogService log) { _log = log; _name = DefaultName; }
}
```

If you genuinely need a configurable secondary value, register it as a typed wrapper (`FooConfig`) and inject that instead of a raw `string`.

### Don't resolve `Lifetime.Singleton` factory-built services from views

Singletons with `Resolution.Lazy` may not be constructed yet when the first MonoBehaviour `[Inject]` runs. The bootstrap pipeline solves this by writing into `IBootstrapProgressReporter`; views read from the reporter, never resolve `BootstrapPipeline` directly. When you add another long-lived service that views need to observe, follow the same write-only / read-only split — don't make views depend on the singleton's factory.

---

## Service / runtime patterns

### Bootstrap step contract

`BootstrapStepBase` provides `Name`, `IsCritical`, `Timeout` (default 30s), `MaxRetries` (default 1). The pipeline:

- Runs steps sequentially in the order declared in `ProjectScopeInstaller.InstallBindings`.
- Wraps each step in a linked CTS that fires `CancelAfter(step.Timeout)`.
- Retries non-critical failures up to `MaxRetries` times, then logs and continues.
- Aborts on critical failure or outer-token cancellation.
- Forwards per-step progress through `IBootstrapProgressReporter` (Singleton in `Zero.Infrastructure`).

When adding a step: extend `BootstrapStepBase`, override `Name`, set `IsCritical` only when the app cannot launch without it (currently only `CrashlyticsStep` is critical), and override `Timeout` for network-bound steps.

### Defensive guards in template-default services

The template ships with empty defaults for many third-party integrations. Any service that wraps a Unity package with required setup (Localization tables, Mobile Notifications channels, etc.) must short-circuit with a warning when the setup is missing — otherwise a fresh clone won't run.

`LocalizationStep` is the canonical example: it guards with `LocalizationSettings.HasSettings` and wraps `InitializationOperation.ToUniTask()` in `try/catch` so a fresh template (no Localization assets, no built Addressables) logs a warning instead of throwing red `InvalidKeyException` errors that look like app-killing problems.

### Sealed services + interface seams

Most service implementations are `sealed`. Extension is via:
1. Replacing the binding in `<Service>ServiceInstaller.Install(...)` (or in a consumer's `<Game>ScopeInstaller.UserServices.cs` partial).
2. Wrapping the existing impl in a decorator and binding the decorator instead.

Documentation should never say "subclass and override" for a `sealed` class — that's an immediate red flag during review.

### `ISaveService.Migrate` is not currently testable

`EncryptedJsonSaveService.Migrate(JObject, int from, int to)` is `private static` and the class is `sealed`. EditMode tests cannot synthesize a v0 envelope and assert the callback fires end-to-end (the shipped tests cover round-trip + tamper-reset only). When a real game adds its first migration, promote `Migrate` to `protected virtual` (or refactor behind an injected `ISaveMigrator` seam) so the migration becomes test-coverable.

---

## Documentation discipline

Tests caught four production bugs in code Phase 1a had marked as "verified." Documentation went through the same gap. Concrete drift caught during Phase 1b review:

- "Override the internal Migrate method" — Migrate is `private static` and class is `sealed`. Cannot override.
- "Subclass `R3EventBus` and override `On<T>`" — class is `sealed`.
- "Extend `EncryptedJsonSaveService`" — class is `sealed`.
- "Record struct events get boxed when stored in `Dictionary<Type, object>`" — wrong: `Subject<T>.OnNext(T)` forwards strongly-typed, no boxing.
- `IL10nService.OnLocaleChanged` documented as `Observable<Locale>` — actual is `Observable<string>` (deliberately so, to keep `Zero.Core` decoupled from `UnityEngine.Localization`).
- `StubLogService` examples had a `Debug(string)` method that doesn't exist on `ILogService`.

**Rule:** every doc claim about a public API must be cross-checked against the actual signature in the source file. AI assistants generating boilerplate docs from training-data familiarity are especially prone to inventing methods that don't exist or assuming standard inheritance patterns. Read the .cs file. Then write the doc.

When you find drift, fix the doc same commit — don't defer.

---

## Wrapping third-party / Unity packages

When you add a new service that wraps a Unity package, a NuGet DLL, or any external SDK, the most expensive bug class is **inventing API shapes from training data instead of reading the actual code**. Phase 2 surfaced this repeatedly. Before writing the wrapper:

### Read the package source — don't guess class or method names

LitMotion lives in `Library/PackageCache/com.annulusgames.lit-motion@*/Runtime/`. The namespace is `LitMotion` (core) and `LitMotion.Extensions` (per-component bindings). The audio-volume extension is `BindToVolume(this MotionBuilder<...>, AudioSource)` — not `BindToAudioSourceVolume`. Inventing the latter gives a CS1061 that AI-flavored fix loops keep failing on because the right name was never in scope.

Unity Mobile Notifications has three asmdefs — `Unity.Notifications.iOS`, `Unity.Notifications.Android`, and the cross-platform `Unity.Notifications.Unified` (preferred). The Unified API exposes `NotificationCenter.Initialize/RequestPermission/ScheduleNotification/CancelScheduledNotification`, the `Notification` struct, and `NotificationsPermissionRequest` whose grant signal is `Status == NotificationsPermissionStatus.Granted` — there is no `Granted` bool property despite that being the obvious name. Read `Library/PackageCache/com.unity.mobile.notifications@*/Runtime/Unified/*.cs` before writing the wrapper, even if you "know" the API.

### asmdef references that don't exist silently break the build

`"LitMotion.Animation"` is not a real asmdef. `"Unity.Notifications"` is not a real asmdef. Both names look plausible; both produce missing-reference diagnostics that are easy to miss in a wall of compile errors. Always cross-check the `references` array against the asmdef files actually present under `Library/PackageCache/<package>/`.

### Check `includePlatforms` on packages you reference

`Unity.Notifications.Unified.asmdef` has `includePlatforms: ["Android", "Editor", "iOS"]`. Any consumer asmdef that references it is fine to compile in Editor and on those targets, but on Standalone / WebGL the reference drops and `using Unity.Notifications;` fails to resolve. Two options:

1. (Preferred for mobile-only template services) Wrap the `using` and every API call body in `#if UNITY_ANDROID || UNITY_IOS || UNITY_EDITOR`, with a no-op `#else` branch that logs and returns safe defaults. The asmdef ref stays in place; Unity ignores it on excluded platforms.
2. Mirror the `includePlatforms` constraint on your wrapper asmdef. Only do this if the entire service can disappear on those platforms — usually it can't, because bootstrap/DI still resolves the binding.

`UnityMobileNotificationService.cs` is the canonical example of option 1.

### Invented constructor parameters cause Reflex `UnknownContractException` at first resolve

If you write a wrapper with a plausible-looking ctor like `public Foo(ILogService log, string defaultId = "x")`, Reflex picks the longest ctor and tries to resolve `string` from the container — which is never bound. Either keep a single ctor with no primitives, or wrap the primitive in a typed config object (`FooConfig`) and bind that. Already covered in "Reflex DI"; mentioned here because it bites every new wrapper you write.

### Cross-check API signatures of injected interfaces before assuming a return shape

`IAssetService.LoadAsync<T>` returns `UniTask<IAssetHandle<T>>`, not `UniTask<T>`. The handle exposes `T Asset { get; }` plus `IDisposable`. Code that writes `T x = await _assetService.LoadAsync<T>(key, ct);` looks idiomatic and will compile against a wrong assumption — except it won't, because the type doesn't match. The same trap exists for `IPoolService.GetPool<T>(T prefab)`: pass a `GameObject` template and you get `IPool<GameObject>`, not `IPool<AudioSource>`. AudioSource has to be fetched via `GetComponent` after `Spawn()`. Confusing the return type was Phase 2's most-repeated bug.

The remedy is the same as the documentation-discipline rule above: open the interface file in `Assets/_Project/Scripts/Runtime/Core/Interfaces/` and read the actual signature. AI assistants generating Service-shaped wrappers from familiarity with similar APIs (Addressables.LoadAssetAsync<T> returning T, Unity Pool returning the prefab type) will reach for the wrong shape almost every time.

### Addressables logs the red exception BEFORE your try/catch sees it

`Addressables.LoadAssetAsync<T>(key)` calls `Debug.LogError` itself when a key isn't registered, then throws `InvalidKeyException`. By the time your `catch` block converts the exception into a friendly warn, the red entry is already in the console. Fresh-clone Bootstrap then looks broken to anyone reading the log — even though the service falls back correctly.

Fix on the consumer side: pre-check key existence with `IAssetService.HasKeyAsync<T>(key, ct)` (backed by `Addressables.LoadResourceLocationsAsync`, which never throws — it returns an empty result list). Only call `LoadAsync` when the pre-check is true. `AudioMixerService.InitializeAsync` is the canonical example.

The same trap applies to any future service that loads optional content via Addressables. Always pre-check; don't rely on try/catch alone.

### Type-name collisions across `using` directives produce silent miscompiles

The runtime asmdefs pull in `R3`, `UnityEngine`, `UnityEngine.InputSystem`, `Unity.Notifications`, etc. Several short type names collide:

- `Touch` exists in both `UnityEngine` (legacy input) and `UnityEngine.InputSystem.EnhancedTouch`. With both `using` directives present, `new Touch[0]` is CS0104 ambiguous. Fully-qualify (`new UnityEngine.InputSystem.EnhancedTouch.Touch[0]`).
- `Notification` exists in both `R3` and `Unity.Notifications`. With R3 in scope (the project default for almost every asmdef), `new Notification { ... }` resolves to the wrong type. Fully-qualify (`new Unity.Notifications.Notification { ... }`) or alias.
- `Object` resolves to `System.Object` when only `using System;` is in scope, but Unity-side calls expect `UnityEngine.Object` (e.g. `Object.Destroy`). Add `using Object = UnityEngine.Object;` at the top of files that mix Unity destruction APIs with `System.Object`-aware code.

Generated wrappers around Unity packages are especially prone to all three because the AI generating them doesn't simulate the full set of `using` directives that the asmdef's references will introduce. Spot-check after each new wrapper.

### Disposable handles must be tracked, not fire-and-forget

`IAssetHandle<T>` is the project's reference-counting contract. Every `LoadAsync` call increments the handle count; the `IAssetService` only releases when you `Dispose` the handle. A long-lived service that loads a clip per call without disposing leaks ref counts and pins assets in memory.

For one-shot loads (e.g. SFX), wrap the play/await/cleanup in `try { ... } finally { handle?.Dispose(); pool.Despawn(go); }` — `OperationCanceledException` from a UniTask token will skip the despawn / dispose otherwise.

For long-lived loads (e.g. mixer, current music clip), store the handle in a field and dispose it in `Dispose()` (or before re-loading the slot).

### Don't use `dynamic` in Unity runtime code

The C# `dynamic` keyword requires `Microsoft.CSharp.dll` and the DLR (Dynamic Language Runtime). **IL2CPP AOT does not support the DLR**, so any code that uses `dynamic` will compile fine in Editor + Mono builds, but fail at link time on iOS App Store, Android IL2CPP, and WebGL deployment.

```csharp
// WRONG — compiles, fails on device IL2CPP builds
dynamic typedHandle = handle;
typedHandle.Cancel();

// RIGHT — use a non-generic interface and explicit type
IPopupHandle typedHandle = handle;
typedHandle.Cancel();
```

If you need dispatch based on a type you can't express in a generic signature, use:
1. A non-generic interface (canonical: `IPopupHandle` with `void Cancel()`).
2. Explicit cast `(IPopupHandle)handle` at the push site.
3. Call the interface method directly on the stack variable.

See `Assets/_Project/Scripts/Runtime/UI/PopupHandle.cs` and `UIService.PopAsync()`.

### Override-sorting child Canvas needs its own GraphicRaycaster

In UGUI, a child Canvas with its own `sortingOrder` (or with `overrideSorting = true`) becomes a new canvas root for raycasting hierarchy. The parent Canvas's `GraphicRaycaster` **does not raycast into** the child canvas — you must add a separate `GraphicRaycaster` to the child.

```csharp
// WRONG — backdrop Canvas has no raycaster, backdrop Image gets no taps
var backdropGo = new GameObject(...);
backdropGo.AddComponent<Canvas>().sortingOrder = sortOrder - 1;
backdropGo.AddComponent<Image>().raycastTarget = true;
// Missing: GraphicRaycaster

// RIGHT
backdropGo.AddComponent<Canvas>().sortingOrder = sortOrder - 1;
backdropGo.AddComponent<GraphicRaycaster>();  // Enables raycasts on this canvas
backdropGo.AddComponent<Image>().raycastTarget = true;
```

The symptom is that `IPointerClickHandler.OnPointerClick()` never fires on the backdrop despite `raycastTarget = true` and a click landing inside its Image bounds. Add the `GraphicRaycaster` to restore the raycasting chain.

See `Assets/_Project/Scripts/Runtime/UI/UIService.CreateBackdrop()`.

---

## Editor-only headless test runs require an exclusive project lock

`Unity -batchmode ... -runTests` cannot open the project if Unity Editor is already running on it. The CLI exits without writing the result XML. There is no friendly error — you just see the launcher log up to "Successfully changed project path" and then nothing. Close the Editor before running CI-like commands, or rely on Test Runner from inside the open Editor.

CI doesn't hit this because `game-ci/unity-test-runner` provisions a fresh Unity in its own container. Local headless runs need either a closed Editor or a separate project clone.

---

## Phase workflow

`docs/dev/PLAN.md` is the source of truth for what should happen; `docs/dev/JOURNAL.md` is the append-only record of what actually shipped each phase. After every phase:

1. **Commit code + tests for that phase.**
2. **Append a JOURNAL.md entry** with files touched, key decisions, deviations from plan, verification status, resume hint.
3. **Refresh `CLAUDE.md`** with anything new that future sessions need to know — new asmdefs, conventions, "easy to miss" items.
4. **Refresh this `PITFALLS.md`** if the phase exposed a new footgun worth recording.
5. **Then merge** to `main` (no-ff).

Skipping any of those before merging means the next session starts with a stale snapshot. CLAUDE.md is loaded automatically; JOURNAL.md is the trail; this file is the warning sign.
