# CLAUDE.md — Game built on Zero template

This project consumes the **Zero** Unity template (`com.tnbao91.nobody.zero`) installed via UPM/OpenUPM. Zero ships ~28 services with mock-first defaults, peer Gameplay/UI/Meta layers, and a Reflex DI bootstrap pipeline. Your job here is to **extend Zero in the consumer's game code** — not modify the package itself.

> Full repo with PLAN/JOURNAL/PITFALLS lives at <https://github.com/tnbao91/unity_zero> for reference. This file is enough for day-to-day work.

## Boundary: what's the package vs what's yours

| Lives in | Owned by | What you do |
|---|---|---|
| `Packages/com.tnbao91.nobody.zero/` | Template author (read-only for you) | **Don't edit.** Updates come via Package Manager. |
| `Assets/_Game/` (or your convention) | You | Game code, prefabs, scenes, your asmdefs reference Zero asmdefs. |
| `Assets/Resources/ZeroSecrets.asset` | You (per-game secret) | Encryption seeds. Gitignored. |

If you find yourself wanting to edit a file inside `Packages/com.tnbao91.nobody.zero/`, stop. Check `claude-context/extension-points.md` first — there's almost always an extension hook (custom installer, custom state, custom command, swap binding) that achieves the same thing without forking.

## Stack constraints (LOCKED — never substitute)

The template's stack is deliberately picked. Do **not** suggest these substitutes:

| Locked | Don't suggest |
|---|---|
| Reflex DI | Zenject, VContainer |
| UniTask | `Task<T>`, coroutines for game logic |
| R3 | UniRx |
| LitMotion | DOTween, PrimeTween |
| `com.unity.nuget.newtonsoft-json` | JsonUtility for non-trivial models |
| ZString | `string.Format`, raw `+` concat for hot paths |
| New Input System | Legacy `Input.touchCount` / `Input.GetKey` (throws on this template's setup) |

Why locked: each was chosen for performance, ergonomics, or compatibility with the rest. Reasoning in the upstream `PLAN.md` §1-2.

## Architecture quick reference

```
Zero.Core (interfaces, POCOs, cross-cutting events)
   ↑
Zero.Infrastructure (BootstrapStepBase, progress reporter)
   ↑
Zero.Services.<Name>  (~24 services, one asmdef each)
   ↑              ↑              ↑
Zero.UI       Zero.Meta       Zero.Gameplay   ← peers, never reference each other
        ↘        ↓        ↙
          Zero.Bootstrap (composition root)
                ↑
       Game.<YourGame>  ← YOUR asmdef goes here
```

**Peer rule:** Gameplay, UI, and Meta cannot reference each other. They cross-talk only through `IEventBus` (impl `R3EventBus`). When YOUR game code adds new domain events, follow the same rule — don't direct-call between game subsystems if they should be loosely coupled.

**DI flow:** Reflex container is built before scene load via `ProjectScopeInstaller.OnRootContainerBuilding`. Services bind in the installer; resolve via `[Inject]` or `Container.RootContainer`. Never `new` a service that has an interface — always inject.

## Available services (cheatsheet)

Inject any of these via Reflex `[Inject]`:

```csharp
ILogService, IDeviceProfileService, ISaveService, IAssetService, ISceneService,
IPoolService, IEventBus, IL10nService, IAudioService, IInputService,
INotificationService, ITimeService, IConsentService, IRemoteConfigService,
IAnalyticsService, IAttributionService, ICrashlyticsService, IAdsService,
IAdPlacementService, IIapService, IReceiptValidator, IVersionCheckService,
IUIService, IBootstrapProgressReporter, IGameStateMachine
```

Detailed signatures + extension points in `claude-context/available-services.md`. SDK adapter recipes (Firebase / AppLovin / Unity IAP / Adjust / etc.) at the upstream repo `docs/services/<name>.md`.

## Common tasks

For each, check `claude-context/extension-points.md` for the recipe:

- **Add a game state** (BootState, MenuState already exist as samples to replace) → impl `IGameState`, register transitions via `IGameStateMachine`.
- **Swap a mock SDK** (real Firebase Crashlytics, AppLovin Ads, Unity IAP) → write `<Real>Service` impl, change one line in the relevant installer.
- **Add a popup** → derive from `PopupBase`, place prefab at Addressables key `ui/popup/<name>` (lowercase).
- **Add a cheat command** → impl `IConsoleCommand` + decorate with `[ConsoleCommand]`.
- **Add a bootstrap step** → derive from `BootstrapStepBase`, add to `steps` array in `ProjectScopeInstaller.cs` order.
- **Persist game state** → use `ISaveService.Set/Get` keyed by string. Don't roll your own. Migration via `Migrate(JObject, fromVersion, toVersion)`.
- **Add a custom service** (per-game `IShopService` etc.) → interface in YOUR asmdef, `<YourGame>ServiceInstaller`, register at scene scope or extend `ProjectScopeInstaller` via partial class.

## Things that bite (top consumer-relevant pitfalls)

Full list in `claude-context/pitfalls.md`. The big ones:

- **`ZeroSecrets.asset` must exist at `Assets/Resources/ZeroSecrets.asset`** with non-placeholder seeds. Player builds throw on startup if missing/unconfigured.
- **`bundleVersion` must be 3-part semver** (e.g. `1.0.0`, not `1.0`) — Unity's default `0.1` makes `VersionCheckService` parse fail and downgrade to `Ok` regardless of remote config.
- **UI requires a `UIRoot`** in your scene with 4 Transform slots (Hud/Popup/Overlay/System) before `IUIService.Push/Show/Toast` works. Throws otherwise. See upstream `docs/ui/ui-root.md`.
- **Tests using `[UnityTest]`** need `using System.Collections;` AND `using UnityEngine.TestTools;`. Async tests use `UniTask.ToCoroutine` pattern, not `[Test] public async UniTask` (silently fails).
- **Lambda subscriptions to `IEventBus.On<T>()`** need `using R3;` — without it, compiler picks `Subscribe(Observer<T>)` and CS1660 every call site.
- **`Object` ambiguity** when using both `using System;` + `using UnityEngine;` — alias `using Object = UnityEngine.Object;` for `Object.Instantiate/Destroy`.

## Workflow

1. **Branch** for feature work (`feature/<topic>` or `phase-<N>` for big rolls).
2. **Add** code in YOUR asmdef. Reference Zero asmdefs you need.
3. **Test** in Editor — Press Play `Bootstrap.unity` (after copying it from Sample import → your scenes folder), check `[Bootstrap] Step N/16` log clean.
4. **Run EditMode tests** — `Window → Test Runner`. Existing Zero tests run alongside yours.
5. **Commit**, conventional-commit style (`feat(shop): add coin pack`).
6. **Update Zero package** when upstream releases — Package Manager → click Update on `com.tnbao91.nobody.zero`. CHANGELOG.md in the package shows what changed.

## When you don't know something

- Specific service shape → `claude-context/available-services.md` or upstream `docs/services/<name>.md`.
- Extension recipe → `claude-context/extension-points.md`.
- Why something is the way it is → upstream `docs/dev/PLAN.md` (architecture rationale) or `docs/dev/JOURNAL.md` (per-phase decisions).
- Footguns → `claude-context/pitfalls.md`.

## What you should NOT do

- Edit anything inside `Packages/com.tnbao91.nobody.zero/`. Updates come via Package Manager and will overwrite your changes.
- Add cross-references between Gameplay/UI/Meta in YOUR game code. Use `IEventBus`.
- Substitute the locked stack.
- Roll your own pool/save/event system when the service exists.
- Suggest a real SDK adapter as a code change to the package — make it the **consumer's installer** swap, in YOUR game's installer file.
