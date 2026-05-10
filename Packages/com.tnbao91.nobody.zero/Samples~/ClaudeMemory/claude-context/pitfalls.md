# Pitfalls (consumer-relevant)

Subset of upstream `docs/dev/PITFALLS.md` filtered to footguns consumers will hit. Each came from a real bug.

## Setup pitfalls

### `ZeroSecrets.asset` must be configured
- File: `Assets/Resources/ZeroSecrets.asset` (rename + move from imported sample).
- Inspector: replace `REPLACE_ME_*` placeholders with random per-game strings.
- Player builds throw `InvalidOperationException` at startup if missing or unconfigured.
- Editor builds warn loud but continue (so iteration isn't blocked).
- Gitignored — these are per-game secrets.

### `bundleVersion` must be 3-part semver
- `ProjectSettings → Player → Version`. Default Unity = `0.1` (2-part).
- `VersionCheckService` parses `Application.version` as `Major.Minor.Patch`. 2-part → parse fail → warn + downgrade to `Ok` regardless of remote `min_version`.
- Bump to `1.0.0` (or whatever) before relying on version gates.

### NuGetForUnity prereq
- R3 has BCL transitive deps (`Microsoft.Bcl.AsyncInterfaces`, etc.) that UPM doesn't manage.
- One-time setup: install `com.github-glitchenzo.nugetforunity`, copy `packages.config` from the imported Bootstrap sample, click `NuGet → Restore Packages`.
- Patched plugin `.meta` files for these DLLs need `Editor.enabled: 1`. NuGetForUnity may revert this on subsequent restores — if R3 disappears from EditMode tests, check the metas first.

### `UIRoot` prerequisite for UI service
- Every scene that uses `IUIService.Push/Show/Toast` needs a `UIRoot` MonoBehaviour with 4 Transform slots (Hud / Popup / Overlay / System).
- Without it, `Push/Show` throw `InvalidOperationException`; `Toast` warn-and-drops.
- Why: framework intentionally does NOT spawn layer canvases — consumer owns scene composition (Phase 3 round 4 decision).
- Recipe at upstream `docs/ui/ui-root.md`.

## C# / Unity language pitfalls

### `record struct` / `init;` don't work
- Unity 6 LTS uses C# 9. `record struct` (C# 10) and `init;` accessors (C# 9 but needs `IsExternalInit`) don't compile.
- For event POCOs / value types: use `readonly struct` with explicit ctor.
- Don't ask Claude to convert to records — it'll break.

### `Object` ambiguity
- `using System;` + `using UnityEngine;` collide on `Object`.
- Symptom: `CS0104: 'Object' is an ambiguous reference between 'System.Object' and 'UnityEngine.Object'`.
- Fix: `using Object = UnityEngine.Object;` at top of file.
- Or: fully qualify (`UnityEngine.Object.Instantiate(prefab)`).

### Async EditMode tests need `UniTask.ToCoroutine`
- `[Test] public async UniTask Foo()` SILENTLY fails. NUnit doesn't await `UniTask`; test reports based on synchronous return.
- Pattern that works:
  ```csharp
  [UnityTest]
  public IEnumerator Foo() => UniTask.ToCoroutine(async () => {
      // your async test body
      Assert.That(...);
  });
  ```
- Plus `using System.Collections;` AND `using UnityEngine.TestTools;` at top.
- Pure-sync tests (`[Test] public void`) don't need this.

### R3 `Subscribe(lambda)` needs `using R3;`
- `Observable<T>.Subscribe(Action<T>)` is a R3 extension method. Without `using R3;`, compiler picks `Subscribe(Observer<T>)` overload and rejects every lambda with `CS1660`.
- Affects every test/script using `_bus.On<X>().Subscribe(evt => ...)`.

## Runtime pitfalls

### Don't `Resolve(commandType)` for unregistered classes
- Reflex throws `UnknownContractException` if the type isn't bound as a contract.
- For classes that take registered services in their ctor but aren't themselves registered (e.g., cheat commands discovered by reflection): use `container.Construct(Type)`.

### Static accessor for the root container
- `Container.RootContainer` (correct).
- `ContainerScope.Root` (wrong — doesn't exist, will error).
- Use this when resolving from a `[RuntimeInitializeOnLoadMethod]`-spawned MonoBehaviour that didn't get `[Inject]`.

### `RegisterFactory` for ctors with non-contract params
- If a service's ctor takes a `string`, `int`, `Func<>`, or any unbound type, `RegisterType` throws at first resolve.
- Example: `VersionCheckService(IRemoteConfigService, ILogService, string localVersion)` — third param needs factory.
- Pattern:
  ```csharp
  builder.RegisterFactory<IVersionCheckService>(c =>
      new VersionCheckService(c.Resolve<IRemoteConfigService>(), c.Resolve<ILogService>(),
                              Application.version),
      Lifetime.Singleton, Resolution.Lazy);
  ```

### `IAssetService.HasKeyAsync` before `LoadAsync` for optional keys
- `Addressables.LoadAssetAsync` calls `Debug.LogError` itself before throwing `InvalidKeyException`.
- Even if your service catches the exception, the red error already logged.
- `HasKeyAsync` wraps `LoadResourceLocationsAsync` — never logs, never throws on missing key.
- Use whenever the key is optional (`AudioMixerService` for the optional mixer asset is the canonical example).

### Concurrent `ChangeStateAsync` throws
- `IGameStateMachine.ChangeStateAsync` rejects concurrent calls with `InvalidOperationException`.
- Caller must `await` previous transition before starting next. No implicit queue.
- Same-instance re-entry (changing back to the SAME state object you're already in) also throws — create a fresh state instance.

## Workflow pitfalls

### Don't edit files inside `Packages/com.tnbao91.nobody.zero/`
- Updates via Package Manager will silently overwrite your edits.
- For overrides: write a re-binding in YOUR installer (last write wins).
- For "I really need to fork this": clone the upstream repo, fork it, point your `manifest.json` at YOUR fork URL.

### Don't add cross-references between Gameplay/UI/Meta
- Even in YOUR game code, follow the peer rule.
- Codex review (and reasonable code review) will flag direct calls between these tiers.
- Use `IEventBus` for cross-tier communication.
