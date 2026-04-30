# Unity Zero — Hybrid Casual Game Template

Opensource greenfield Unity 6 LTS template for hybrid casual and puzzle games. Minimal, production-ready infrastructure with extension points for rapid game iteration. Meta loop (wallet / progression / rewards) is intentionally out of scope — see `docs/meta/recipes.md` for per-game patterns.

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Unity 6.0.3.11f1](https://img.shields.io/badge/Unity-6.0.3.11f1-black.svg)](ProjectSettings/ProjectVersion.txt)

## Stack (locked, do not substitute)

| Concern | Pick | Why over alternatives |
|---|---|---|
| DI | [Reflex](https://github.com/gustavopsantos/Reflex) | Source-only, fastest, minimal API |
| Async | [UniTask](https://github.com/Cysharp/UniTask) | Zero-alloc, PlayerLoop-aware (not `Task<T>`) |
| Reactive | [R3](https://github.com/Cysharp/R3) | Successor to UniRx, struct-based observables |
| Tweening | [LitMotion](https://github.com/AnnulusGames/LitMotion) | Burst/jobs, fastest mobile tween (not DOTween) |
| JSON | Newtonsoft.Json (NuGetForUnity) | Battle-tested, Unity package available |
| String building | [ZString](https://github.com/Cysharp/ZString) | Zero-alloc string interpolation |
| Localization | `com.unity.localization` | Wraps the official Unity package |
| Notifications | `com.unity.mobile.notifications` | Wraps the official Unity Unified API |
| Object pool | `UnityEngine.Pool.ObjectPool` | Wraps the built-in Unity pool |

## Architecture

```
Zero.Core (interfaces, POCOs, cross-cutting events)
   ↑
Zero.Infrastructure (BootstrapStepBase + progress reporter)
   ↑
Zero.Services.<Name>  (one asmdef per service)
   ↑              ↑              ↑
Zero.UI       Zero.Meta       Zero.Gameplay   ← peers, talk via IEventBus
        ↘        ↓        ↙
          Zero.Bootstrap (composition root)
```

Gameplay/Meta/UI never reference each other directly — cross-tier coupling goes through `IEventBus` (impl `R3EventBus`). Composition root `Zero.Bootstrap` is the only asmdef that references all three peers. Full DAG diagram in [docs/architecture/asmdef-graph.md](docs/architecture/asmdef-graph.md).

## Quick Start

1. **Clone** the repository.
2. **Open** the project in Unity 6.0.3.11f1 (matching `ProjectSettings/ProjectVersion.txt`). Do not let Unity upgrade the project on a different LTS.
3. **Restore NuGet packages** if `Newtonsoft.Json` or R3 transitive deps are missing — `NuGet → Restore Packages` (NuGetForUnity menu). Patched plugin metas in this repo enable `Editor.enabled` for R3 + transitive deps; do not let NuGet revert those.
4. **Create the encryption seed asset.** Copy `Assets/Resources/ZeroSecrets.asset.example` to `Assets/Resources/ZeroSecrets.asset`, open it in the Inspector, and replace the `REPLACE_ME_*` placeholder strings with random bytes. The new file is gitignored. Player builds throw on startup if it is missing or unconfigured. See [docs/security/save-encryption.md](docs/security/save-encryption.md).
5. **Open `Assets/_Project/Scenes/Bootstrap.unity`** and press Play. The bootstrap pipeline log lines (`[Bootstrap] Step N/M: ...`) appear in the Console.
6. **Run EditMode tests** via `Window → General → Test Runner` to verify the install. ~55 cases should be green.
7. **Read `CLAUDE.md`** before extending — it indexes every footgun and convention. Pair with [docs/dev/PITFALLS.md](docs/dev/PITFALLS.md).

## Phase status

All five build phases are complete and merged to `main`. See [docs/dev/JOURNAL.md](docs/dev/JOURNAL.md) for per-phase deltas + lessons learned.

| Phase | Scope | Status |
|---|---|---|
| 1a / 1b | Foundation: asmdef restructure, event bus, localization wrap, save hardening, pool refactor, tests + CI | Done |
| 2 | Real Input + Audio + Notification (wrap Unity packages) | Done |
| 3 | UI scaffolding: popup stack, screens, transitions, toast, localized text, consumer-owned `UIRoot` | Done |
| 4 | Gameplay scaffolding: state machine, level loader, lifecycle events, sample states | Done |
| 5a | Live-Ops: VersionCheck service + bootstrap step. DevTools: cheat console + FPS overlay (gated to Editor / DEVELOPMENT_BUILD) | Done |
| 5b | Cross-cutting docs: this README + LICENSE + CONTRIBUTING + 8 Mock SDK extension recipes + meta recipes | Done |

## Documentation

- **Architecture** — [event bus](docs/architecture/event-bus.md), [bootstrap pipeline](docs/architecture/bootstrap-pipeline.md), [asmdef graph](docs/architecture/asmdef-graph.md).
- **Services (real impls)** — [save](docs/services/save.md), [localization](docs/services/localization.md), [pool](docs/services/pool.md), [audio](docs/services/audio.md), [input](docs/services/input.md), [notification](docs/services/notification.md), [version-check](docs/services/version-check.md), [time](docs/services/time.md).
- **Services (mocks + extension recipes)** — [crashlytics](docs/services/crashlytics.md), [consent](docs/services/consent.md), [remote-config](docs/services/remote-config.md), [analytics](docs/services/analytics.md), [attribution](docs/services/attribution.md), [ads](docs/services/ads.md), [iap](docs/services/iap.md), [receipt-validator](docs/services/receipt-validator.md).
- **UI** — [popup-stack](docs/ui/popup-stack.md), [ui-root](docs/ui/ui-root.md), [loading-screen](docs/ui/loading-screen.md), [safe-area](docs/ui/safe-area.md), [toast](docs/ui/toast.md), [localized-text](docs/ui/localized-text.md).
- **Gameplay** — [state-machine](docs/gameplay/state-machine.md), [level-loading](docs/gameplay/level-loading.md).
- **Live-Ops** — [version-check flow](docs/liveops/version-check.md), [Addressables remote](docs/liveops/addressables-remote.md).
- **DevTools** — [cheat console](docs/dev/cheat-console.md), [FPS overlay](docs/dev/fps-overlay.md).
- **Security** — [save encryption](docs/security/save-encryption.md).
- **Testing** — [writing tests](docs/testing/writing-tests.md), [CI](docs/testing/ci.md), [manual checklist](docs/testing/manual-checklist.md).
- **Meta recipes** (no impl ships) — [docs/meta/recipes.md](docs/meta/recipes.md).
- **Dev** — [PLAN.md](docs/dev/PLAN.md), [JOURNAL.md](docs/dev/JOURNAL.md), [PITFALLS.md](docs/dev/PITFALLS.md).
- **Vietnamese** — [README.vi.md](README.vi.md).

## CI Setup

EditMode tests run on push via GitHub Actions (`.github/workflows/tests.yml`). Required: activate Unity headlessly by setting either `UNITY_LICENSE` (Pro/Plus, base64-encoded `.ulf`) **or** the trio `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` (Personal). See [game-ci activation docs](https://game.ci/docs/github/activation) and [docs/testing/ci.md](docs/testing/ci.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the service-add recipe, test conventions, and review expectations. The plan + journal in `docs/dev/` are the source of truth for architectural decisions.

## License

MIT — see [LICENSE](LICENSE).
