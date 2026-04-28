# Unity Zero — Hybrid Casual Game Template

Opensource greenfield Unity 6 LTS template for hybrid casual and puzzle games. Minimal, production-ready infrastructure with extension points for rapid game iteration.

## Quick Start

1. Clone the repository.
2. Open the project in Unity 6.0.3.11f1 (matching `ProjectSettings/ProjectVersion.txt`).
3. Open `Assets/_Project/Scenes/Bootstrap.unity` and press Play to run the bootstrap pipeline.

## CI Setup

EditMode tests run on push via GitHub Actions (`.github/workflows/tests.yml`).

**Required:** activate Unity headlessly by setting either `UNITY_LICENSE` (Pro/Plus, base64-encoded `.ulf`) **or** the trio `UNITY_EMAIL` / `UNITY_PASSWORD` / `UNITY_SERIAL` (Personal). See [game-ci activation docs](https://game.ci/docs/github/activation) and `docs/testing/ci.md` for the step-by-step.

## Documentation

- **Architecture:** `docs/architecture/` — event bus, bootstrap pipeline, asmdef graph.
- **Services:** `docs/services/` — save, localization, pool, and extension recipes.
- **Security:** `docs/security/` — encryption model and per-game secret setup.
- **Testing:** `docs/testing/` — EditMode test patterns and CI workflow.
- **Plan & Journal:** `docs/dev/PLAN.md`, `docs/dev/JOURNAL.md` — architectural decisions and implementation phases.

## What's Included

- **Core Services:** dependency injection (Reflex), save/encryption, asset loading (Addressables), localization (Unity Localization Package), object pooling.
- **Infrastructure:** bootstrap pipeline with timeouts/retries/progress, event bus for cross-asmdef communication.
- **Scaffolding:** empty peer layers (UI, Gameplay, Meta) ready for per-game extension.

## What's Out of Scope

- Meta layer (wallet, progression, rewards) — consumer-specific; reference recipes ship in Phase 5 (`docs/meta/recipes.md`).
- Demo game — template is genre-agnostic.
- Real SDK implementations (Ads, IAP, Analytics, etc.) — mocks provided; real adapters via extension points.

## License

MIT
