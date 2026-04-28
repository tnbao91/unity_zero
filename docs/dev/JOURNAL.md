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
- Verification gap: Editor compile + Press Play loop NOT verified (Claude cannot open Unity). Asmdef DAG verified by inspection: no `Zero.UI`/`Zero.Meta` refs in `Zero.Gameplay.asmdef`; no `Zero.Meta` ref in `Zero.UI.asmdef`. `grep -rn ReflexPoolService Assets/` returns zero matches. **User must Press Play once on `Bootstrap.unity` and confirm log shows new step order before declaring 1a green.**
- Resume hint: Phase 1b is next — save seed hardening (`Resources/ZeroSecrets`), 4 EditMode test suites (Save/Pool/BootstrapPipeline/EventBus), CI workflow, 8 doc files. Phase 1b should land on a new branch `phase-1b-tests-docs` once 1a is merged.

---
