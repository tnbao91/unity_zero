# Implementation Journal — Unity Zero v2

Append-only log of phase implementations. One entry per phase complete.

**Format:**
```
## Phase <N> — YYYY-MM-DD (commit <sha-short>)
- Branch: <branch-name>
- Files touched: <list>
- Key decisions: <deviations from plan if any>
- Tests: <X/Y passing>
- Resume hint: <next phase prerequisite, known gotchas>
```

**Source of truth for plan:** [`PLAN.md`](./PLAN.md). Read this journal's tail + the plan to resume work after a session clear.

---

## Phase 1a — 2026-04-28 (commit c0ee281)
- Branch: `phase-1a-foundation`
- Files touched:
  - New interfaces in `Zero.Core`: `IEventBus`, `IL10nService`, `IBootstrapProgressReporter`; expanded `IInputService`; added `Timeout`/`MaxRetries` to `IBootstrapStep`.
  - New asmdef `Zero.Services.Events` with `R3EventBus` (typed `Dictionary<Type, object>` storing `Subject<T>`).
  - New asmdef `Zero.Services.Localization`: `UnityLocalizationService` (wraps `com.unity.localization`), `MockLocalizationService`, `LocalizationStep` (awaits `LocalizationSettings.InitializationOperation`).
  - `Zero.Infrastructure.BootstrapProgressReporter` (Singleton; pipeline writes, views read).
  - `BootstrapPipeline`: per-step `Progress<float>` slicing, `CancelAfter(step.Timeout)`, retry loop honoring `MaxRetries` for non-critical steps.
  - `ProjectScopeInstaller`: registered Events + Localization installers; reordered step list (Save now right after DeviceProfile per PLAN 2.11).
  - Asmdef peer restructure: `Zero.Gameplay`/`Zero.Meta`/`Zero.UI` no longer cross-reference each other; all reference `Zero.Services.Events` instead.
  - Pool refactor: `ReflexPoolService` → `UnityPoolService`, internal storage `Stack<GameObject>` → `UnityEngine.Pool.ObjectPool<GameObject>` with compile-time-gated `collectionCheck`. `IPoolService` interface unchanged.
- Key decisions:
  - Pool `actionOnGet` left null; `SetActive(true)` happens in `Spawn()` *after* `SetParent` + `SetPositionAndRotation` so `OnEnable` sees the requested transform (fixed in c0ee281 after initial refactor activated too early).
  - `createFunc` instantiates parented to `[Zero.Pools]` root and immediately `SetActive(false)` to prevent the just-created object flashing active before first Spawn.
  - Pool defaults: `defaultCapacity: 10`, `maxSize: 10000` — to be documented in `docs/services/pool.md` during Phase 1b.
- Tests: none yet (Phase 1b ships the 4 EditMode test suites).
- Late-phase fixes (after first Press Play surfaced compile + runtime errors):
  - `Zero.Services.Localization.asmdef` — added `Unity.ResourceManager` (for `AsyncOperationHandle<>`) and `UniTask.Addressables` (for `.ToUniTask()` extension on it).
  - `UnityLocalizationService` — collapsed to single `(ILogService)` ctor; the `(ILogService, string)` overload made Reflex try to resolve `String` from the container and fail.
  - `LocalizationStep` — guard with `LocalizationSettings.HasSettings` + try/catch so a fresh template (no Localization assets / Addressables not built) logs a warning instead of red `InvalidKeyException`.
- Verification: User confirmed Editor compile clean + Bootstrap.unity Press Play runs through all steps with no console errors. Asmdef DAG verified: no `Zero.UI`/`Zero.Meta` refs in `Zero.Gameplay.asmdef`; no `Zero.Meta` ref in `Zero.UI.asmdef`. `grep -rn ReflexPoolService Assets/` returns zero matches.
- CLAUDE.md refreshed alongside this entry (commit `e771e95`) — peer asmdef diagram, bootstrap timeout/retry, real-impl exceptions list.
- Resume hint: Phase 1b is next — save seed hardening (`Resources/ZeroSecrets`), 4 EditMode test suites (Save/Pool/BootstrapPipeline/EventBus), CI workflow, 8 doc files. Phase 1b should land on a new branch `phase-1b-tests-docs`.

---

## Phase 1b — 2026-04-28 (commits babace5, 2557802, 234b54a, 8688cd8)
- Branch: `phase-1b-tests-docs`
- Files touched:
  - New: `Assets/_Project/Scripts/Runtime/Services/Save/ZeroSecrets.cs` (ScriptableObject for per-game seeds)
  - New: `Assets/Resources/ZeroSecrets.asset.example` (template with placeholder marker)
  - New: 4 EditMode test files (SaveServiceTests, PoolServiceTests, BootstrapPipelineTests, EventBusTests)
  - New: `.github/workflows/tests.yml` (game-ci/unity-test-runner, EditMode only)
  - New: `README.md` (minimal, mentions CI + Quick Start)
  - New: 8 module documentation files under `docs/`:
    - `docs/architecture/event-bus.md`, `docs/architecture/bootstrap-pipeline.md`
    - `docs/services/save.md`, `docs/services/localization.md`, `docs/services/pool.md`
    - `docs/security/save-encryption.md`
    - `docs/testing/writing-tests.md`, `docs/testing/ci.md`
  - Edit: `EncryptedJsonSaveService` — read seeds from `Resources/ZeroSecrets`; throw in player builds if missing/placeholder; warn + fallback in Editor.
  - Edit: `Zero.Tests.EditMode.asmdef` — added service references needed by tests.
  - Edit: `CLAUDE.md` — refreshed save seed section, added CI notes, added docs notes.
- Key decisions:
  - Tests use actual `Application.persistentDataPath` but clean up in `[TearDown]` to avoid pollution.
  - Event tests verify typed `Subject<T>` in bus works; record struct events fully supported (no boxing caveat in docs).
  - Bootstrap tests cover full pipeline: order, abort, swallow, progress, cancel, timeout, retry.
  - Documentation format locked: every module doc has Overview (2-3 sent), Public API, Extension Points, Examples, Known Limitations, Design Rationale.
  - README is minimal (no full setup guide — that's Phase 5 cross-cutting); mentions CI license requirement.
- Tests: 4 test suites, ~22 test cases total. Cover SaveService round-trip + tamper + reload smoke; PoolService LIFO + prewarm + active/inactive flags + dispose idempotency; BootstrapPipeline order + critical-abort + non-critical-swallow + monotonic progress + outer-cancel + critical-timeout + retry-then-success; EventBus pub/sub + multi-sub + type isolation + dispose + value-type + late-subscriber.
- Opus review (commit `22eb399`): the four test files Haiku originally produced did **not** compile against the real public APIs (wrong `OnExecuteAsync` signature, wrong `RunAsync` arity, `IPool<T>.Get/Release` instead of `Spawn/Despawn`, `IPoolService.GetPool<T>()` missing prefab arg, `StubLogService` missing `IsEnabled` + had a fictional `Debug` method). Two assertions were also logically wrong (`TimeoutFires` expected non-critical timeout to abort the pipeline; `RetryPolicy` had off-by-one on the flaky-step counter). All four suites rewritten end-to-end against the actual contracts. Pool doc and bootstrap-pipeline doc signatures corrected. `ZeroSecrets.cs.meta` pre-committed with deterministic GUID so `ZeroSecrets.asset.example` resolves the script reference on first project open.
- Migration test gap: `EncryptedJsonSaveService.Migrate(...)` is `private`, so the Phase 1b plan's "write v0 file → assert Migrate ran" coverage isn't reachable from the test asmdef. Replaced with a load→save→reload smoke test plus a "Known Limitations" note in `docs/services/save.md`. Promoting `Migrate` to `protected virtual` is a Phase 2+ candidate if migration coverage matters.
- Verification: code-review only — Claude can't open Unity Test Runner. **User must run `Window → General → Test Runner → EditMode → Run All` once** and confirm the four suites are green before merging Phase 1b to main.
- Resume hint: Phase 2 — real Input + Audio + Notification services + manual checklist + integration tests on branch `phase-2-real-services`.

---
