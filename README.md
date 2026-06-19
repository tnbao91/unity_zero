# Zero — Hybrid Casual Unity Template

[![License: MIT](https://img.shields.io/badge/License-MIT-yellow.svg)](LICENSE)
[![Unity 6000.5.0f1](https://img.shields.io/badge/Unity-6000.5.0f1-black.svg)](ProjectSettings/ProjectVersion.txt)
[![openupm](https://img.shields.io/npm/v/com.tnbao91.nobody.zero?label=openupm&registry_uri=https://package.openupm.com)](https://openupm.com/packages/com.tnbao91.nobody.zero/)

Opinionated Unity 6 LTS template for hybrid casual / puzzle games. Distributed as a Unity Package Manager package — install once, update via Package Manager. Stack: Reflex DI · UniTask · R3 · LitMotion · Addressables · Newtonsoft · ZString · New Input System.

> **Meta layer (wallet / progression / rewards) is intentionally out of scope** — hybrid casual and puzzle have different meta loops. See [`docs/meta/recipes.md`](docs/meta/recipes.md) for per-game patterns.

---

This repository serves two audiences:

- **Game developers** consuming the template → [Install via Package Manager](#install-via-package-manager).
- **Contributors** to the package itself → [Develop in this repo](#develop-in-this-repo).

---

## Install via Package Manager

### 1. Add scoped registries

In your project's `Packages/manifest.json`:

```jsonc
{
  "scopedRegistries": [
    {
      "name": "OpenUPM",
      "url": "https://package.openupm.com",
      "scopes": [
        "com.cysharp",
        "com.gustavopsantos",
        "com.annulusgames",
        "com.tnbao91",
        "com.github-glitchenzo"
      ]
    }
  ]
}
```

### 2. Install NuGetForUnity (prereq for R3 BCL transitive deps)

`Window → Package Manager → "+" → Add package by name` → `com.github-glitchenzo.nugetforunity`.

### 3. Install Zero

```sh
openupm add com.tnbao91.nobody.zero
```

Or `Add package by name` → `com.tnbao91.nobody.zero`.

> Latest is `0.5.0`. Pin `0.2.1` or later — tags `v0.1.0` / `v0.2.0` are broken (missing `Runtime/Services/Log/` from a `.gitignore` collision); see the [CHANGELOG](CHANGELOG.md) `0.2.1` entry.

### 4. Import Bootstrap sample

`Package Manager → Zero → Samples → Import "Bootstrap Scene"`. Files land in `Assets/Samples/com.tnbao91.nobody.zero/<version>/BootstrapScene/`. Then:

- Move `Bootstrap.unity` to your scenes folder, add to Build Settings.
- Move `ReflexSettings.asset` to `Assets/Resources/`.
- Copy `packages.config` to `Assets/`, then `NuGet → Restore Packages` to fetch the 4 BCL DLLs R3 needs.

### 5. Configure save encryption seeds

- Move `ZeroSecrets.asset.example` to `Assets/Resources/`, rename to `ZeroSecrets.asset`.
- Replace placeholder seeds in the Inspector with per-game random strings. Do **not** commit them.

### 6. (Optional) Import Claude Memory sample for AI agents

If you use Claude Code, also `Import "Claude Memory"` sample. Move the imported `CLAUDE.md` + `claude-context/` to your repo root. Claude Code auto-reads them at session start, so the agent knows your stack constraints, available services, extension recipes, and consumer-relevant pitfalls without re-deriving them every conversation. See [`Samples~/ClaudeMemory/README.md`](Packages/com.tnbao91.nobody.zero/Samples~/ClaudeMemory/README.md) inside the package.

### 7. Press Play

Open `Bootstrap.unity` → Press Play. Console logs `[Bootstrap] Step N/16: ...` for every pipeline step.

Full Quick Start in the [package README](Packages/com.tnbao91.nobody.zero/README.md).

---

## Develop in this repo

This repository is a Unity 6 LTS dev project that **embeds** the package at `Packages/com.tnbao91.nobody.zero/`. Clone, open, edit, test — Unity discovers the embedded package automatically.

### Stack (locked, do not substitute)

| Concern | Pick | Why over alternatives |
|---|---|---|
| DI | [Reflex](https://github.com/gustavopsantos/Reflex) | Source-only, fastest, minimal API |
| Async | [UniTask](https://github.com/Cysharp/UniTask) | Zero-alloc, PlayerLoop-aware (not `Task<T>`) |
| Reactive | [R3](https://github.com/Cysharp/R3) | Successor to UniRx, struct-based observables |
| Tweening | [LitMotion](https://github.com/AnnulusGames/LitMotion) | Burst/jobs, fastest mobile tween (not DOTween) |
| JSON | `com.unity.nuget.newtonsoft-json` | Battle-tested, Unity-shipped (no NuGet for the JSON dep) |
| String building | [ZString](https://github.com/Cysharp/ZString) | Zero-alloc string interpolation |
| Localization | `com.unity.localization` | Wraps the official Unity package |
| Notifications | `com.unity.mobile.notifications` | Wraps the official Unity Unified API |
| Object pool | `UnityEngine.Pool.ObjectPool` | Wraps the built-in Unity pool |

### Architecture

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

### Dev workspace Quick Start

1. **Clone**: `git clone https://github.com/tnbao91/unity_zero.git`.
2. **Open** in Unity 6000.5.0f1 (matching `ProjectSettings/ProjectVersion.txt`). Do not let Unity upgrade the project on a different LTS.
3. **NuGet**: `NuGet → Restore Packages` (NuGetForUnity menu) for R3 BCL transitive deps. Patched plugin metas in this repo enable `Editor.enabled` for R3 + transitive deps; do not let NuGet revert those.
4. **Encryption seed**: copy `Assets/Resources/ZeroSecrets.asset.example` to `ZeroSecrets.asset`, replace placeholders. Gitignored.
5. **Open** `Assets/_Project/Scenes/Bootstrap.unity` → Press Play. Pipeline runs all 16 bootstrap steps.
6. **Tests**: `Window → General → Test Runner`. ~79 EditMode cases should be green.
7. **Read `CLAUDE.md`** before extending — it's the project constitution (principles + anti-patterns + references). Module detail lives in `docs/`; footguns in [`docs/dev/PITFALLS.md`](docs/dev/PITFALLS.md).

### Pre-commit hooks

Lint + formatting hygiene is enforced by the [`pre-commit`](https://pre-commit.com) framework. **Run once after clone:**

```bash
pip install pre-commit   # or: brew install pre-commit
pre-commit install
```

From then on, every `git commit` runs the hooks in [`.pre-commit-config.yaml`](.pre-commit-config.yaml): trailing whitespace, EOL normalization, YAML/JSON validation, merge-conflict markers, large-file guard, and `dotnet format whitespace --verify-no-changes` (auto-skipped on fresh clone until Unity regenerates `*.sln`). Style rules live in [`.editorconfig`](.editorconfig); dead-code diagnostics (`IDE0051`, `IDE0052`, `CS0414`, `CS0219`) are set to `error` severity. CI mirrors the same hooks in [`.github/workflows/lint.yml`](.github/workflows/lint.yml), so bypassing locally with `--no-verify` will still be caught.

> Roslyn analyzer DLLs (Meziantou.Analyzer / Microsoft.CodeAnalysis.NetAnalyzers) for full dead-code detection are a separate, Unity-Editor step — see [`docs/dev/PITFALLS.md`](docs/dev/PITFALLS.md) "Plugin metas" before installing.

### Repo layout

```
unity_zero/
├── Assets/                         ← dev workspace (Bootstrap.unity, Resources, ProjectSettings)
├── Packages/
│   ├── manifest.json
│   └── com.tnbao91.nobody.zero/    ← THE PACKAGE (Runtime, Tests, Samples~, Documentation~)
├── docs/                           ← user-facing docs (rendered on GitHub)
├── .github/                        ← workflows, templates
├── README.md                       ← you are here
├── CLAUDE.md                       ← contributor doc (not in package)
├── CHANGELOG.md
├── CONTRIBUTING.md
└── LICENSE
```

When editing the package: the dev `Assets/_Project/Scenes/Bootstrap.unity` is the canonical scene. Before tagging a release, copy changes to `Packages/com.tnbao91.nobody.zero/Samples~/BootstrapScene/Bootstrap.unity`. See [CONTRIBUTING.md](CONTRIBUTING.md) for the sync convention.

## Documentation

- **Architecture** — [event bus](docs/architecture/event-bus.md), [bootstrap pipeline](docs/architecture/bootstrap-pipeline.md), [asmdef graph](docs/architecture/asmdef-graph.md).
- **Services (real impls)** — [save](docs/services/save.md), [localization](docs/services/localization.md), [pool](docs/services/pool.md), [audio](docs/services/audio.md), [input](docs/services/input.md), [notification](docs/services/notification.md), [version-check](docs/services/version-check.md), [time](docs/services/time.md).
- **Services (infra & policy)** — [asset](docs/services/asset.md), [scene](docs/services/scene.md), [log](docs/services/log.md), [device-profile](docs/services/device-profile.md), [events](docs/services/events.md), [adplacement](docs/services/adplacement.md).
- **Services (mocks + extension recipes)** — [crashlytics](docs/services/crashlytics.md), [consent](docs/services/consent.md), [remote-config](docs/services/remote-config.md), [analytics](docs/services/analytics.md), [attribution](docs/services/attribution.md), [ads](docs/services/ads.md), [iap](docs/services/iap.md), [receipt-validator](docs/services/receipt-validator.md).
- **UI** — [popup-stack](docs/ui/popup-stack.md), [ui-root](docs/ui/ui-root.md), [loading-screen](docs/ui/loading-screen.md), [safe-area](docs/ui/safe-area.md), [toast](docs/ui/toast.md), [localized-text](docs/ui/localized-text.md).
- **Gameplay** — [state-machine](docs/gameplay/state-machine.md), [level-loading](docs/gameplay/level-loading.md).
- **Live-Ops** — [version-check flow](docs/liveops/version-check.md), [Addressables remote](docs/liveops/addressables-remote.md).
- **DevTools** — [cheat console](docs/dev/cheat-console.md), [FPS overlay](docs/dev/fps-overlay.md).
- **Security** — [save encryption](docs/security/save-encryption.md).
- **Testing** — [writing tests](docs/testing/writing-tests.md), [CI](docs/testing/ci.md), [manual checklist](docs/testing/manual-checklist.md).
- **Meta recipes** (no impl ships) — [docs/meta/recipes.md](docs/meta/recipes.md).
- **Dev (contributor only)** — [PLAN.md](docs/dev/PLAN.md), [JOURNAL.md](docs/dev/JOURNAL.md), [PITFALLS.md](docs/dev/PITFALLS.md), [AGENT-WORKFLOW.md](docs/dev/AGENT-WORKFLOW.md).

## CI Setup

EditMode tests run on push via GitHub Actions (`.github/workflows/tests.yml`). Required: activate Unity headlessly by setting either `UNITY_LICENSE` (Pro/Plus, base64-encoded `.ulf`) **or** the trio `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` (Personal). See [game-ci activation docs](https://game.ci/docs/github/activation) and [docs/testing/ci.md](docs/testing/ci.md).

## Contributing

See [CONTRIBUTING.md](CONTRIBUTING.md) for the service-add recipe, test conventions, and review expectations. The plan + journal in `docs/dev/` are the source of truth for architectural decisions.

## License

MIT — see [LICENSE](LICENSE).
