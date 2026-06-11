# CLAUDE.md — Game built on Zero template

Guidance for Claude Code in a project that consumes the **Zero** Unity template (`com.tnbao91.nobody.zero`) via UPM/OpenUPM.

> **This file is a constitution.** Only principles + anti-patterns + references. Service signatures, extension recipes, and footgun details live in `claude-context/`. Before adding a sentence here, ask: *"Is this an invariant principle, or a lookup detail?"* If lookup, put it in the right `claude-context/*.md` file and link.

> Full upstream repo with `PLAN.md` / `JOURNAL.md` / `PITFALLS.md` at <https://github.com/tnbao91/unity_zero>.

## Boundary: what's the package vs what's yours

| Lives in | Owned by | What you do |
|---|---|---|
| `Packages/com.tnbao91.nobody.zero/` | Template author (read-only for you) | **Don't edit.** Updates come via Package Manager. |
| `Assets/_Game/` (or your convention) | You | Game code, prefabs, scenes, your asmdefs reference Zero asmdefs. |
| `Assets/Resources/ZeroSecrets.asset` | You (per-game secret) | Encryption seeds. Gitignored. |

If you find yourself wanting to edit a file inside `Packages/com.tnbao91.nobody.zero/`, stop. The extension hook (custom installer, custom state, custom command, swap binding) almost always exists — see `claude-context/extension-points.md`.

## Core principles

- **Peer rule.** `Zero.Gameplay`, `Zero.Meta`, `Zero.UI` (and your equivalents in YOUR game asmdef) never reference each other. Cross-tier coupling goes through `IEventBus` (impl `R3EventBus`).
- **DI via Reflex.** Resolve services via `[Inject]` or `Container.RootContainer`. Never `new` a service that has an interface — always inject. Custom per-game services go in YOUR own installer: subscribe `ContainerScope.OnRootContainerBuilding` from your asmdef (recipe in `claude-context/extension-points.md` §7), or register at scene scope. Do NOT try a `ProjectScopeInstaller` partial — C# partials cannot span assemblies, so that path does not exist for UPM consumers.
- **Sealed services + interface seams.** Every Zero service impl is `sealed`. Extend by replacing the binding (in your installer) or by wrapping in a decorator — never "subclass and override".
- **Mock-first SDK swap.** Real SDK adapter is a one-line installer swap in YOUR game's installer. Don't fork the package to inject a real Crashlytics / Ads / IAP impl.
- **Test behavior, not snapshots.** A test that asserts consumer-facing behavior (state machine transition order, save round-trip, gesture detection at documented thresholds) earns its weight. Snapshot-style tests that instantiate-and-null-check do not.

## Anti-patterns (do not)

- Cross-reference Gameplay ↔ Meta ↔ UI in YOUR game code. Use `IEventBus`.
- Substitute the locked stack — Reflex/UniTask/R3/LitMotion/Newtonsoft/ZString/New Input System. See `claude-context/stack-constraints.md` for pushback rules.
- Roll your own pool/save/event/notification/audio service when `IPoolService` / `ISaveService` / `IEventBus` / `INotificationService` / `IAudioService` already exists.
- Use legacy `Input.*` API. Active Input Handling is "Input System Package" only — legacy calls throw.
- `[Test] public async UniTask` for async EditMode tests. Use `[UnityTest] public IEnumerator Foo() => UniTask.ToCoroutine(async () => { ... });`.
- Subscribe to `IEventBus.On<T>()` via lambda without `using R3;` at the top of the file. The lambda binds to the wrong overload (CS1660).
- Use `dynamic` in Runtime code. IL2CPP/AOT does not support the DLR.
- Edit anything inside `Packages/com.tnbao91.nobody.zero/`. Package updates will overwrite your edits.
- Edit a test to make it agree with the spec when the two disagree. Tests are the executable spec and the final authority — fix the prose spec, never weaken the test to match it.

## Workflow

1. Branch for feature work (`feature/<topic>`).
2. Add code in YOUR asmdef. Reference Zero asmdefs you need.
3. Verify in Editor — Press Play `Bootstrap.unity`, check `[Bootstrap] Step N/M` log clean.
4. Run EditMode tests (`Window → Test Runner`).
5. Commit (conventional-commit style: `feat(shop): add coin pack`).
6. Update Zero package via Package Manager when upstream releases; read package `CHANGELOG.md` for breaking changes.

## References

- `claude-context/architecture.md` — asmdef tier diagram, peer rule, DI flow, bootstrap order.
- `claude-context/available-services.md` — every `I*Service` interface signature you can inject. Open this when you need to know *what to call*.
- `claude-context/extension-points.md` — 8 concrete recipes (swap mock SDK, add state, add popup, add cheat command, add bootstrap step, persist data, add custom service, subscribe to lifecycle events).
- `claude-context/stack-constraints.md` — locked picks + pushback when AI suggests substitutes.
- `claude-context/pitfalls.md` — consumer-relevant footguns. **Required reading** before extending UI, tests, or anything that touches DI / Addressables / save.
- Upstream `docs/services/<name>.md` for SDK adapter recipes (Firebase / AppLovin / Unity IAP / Adjust / etc.).
- Upstream `docs/dev/PLAN.md` / `JOURNAL.md` / `PITFALLS.md` for full architectural rationale and history.
