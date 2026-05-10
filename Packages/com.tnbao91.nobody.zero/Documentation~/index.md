# Zero — Documentation

The full documentation is hosted at the GitHub repository:

<https://github.com/tnbao91/unity_zero/tree/main/docs>

This package keeps docs at the repo root rather than bundling them in the tarball, so:

- GitHub renders the markdown as the primary source.
- One source of truth for module docs (no drift between tarball and web).
- Smaller package install footprint.

## Sections

- **Architecture**: [event-bus](https://github.com/tnbao91/unity_zero/blob/main/docs/architecture/event-bus.md) · [bootstrap-pipeline](https://github.com/tnbao91/unity_zero/blob/main/docs/architecture/bootstrap-pipeline.md) · [asmdef-graph](https://github.com/tnbao91/unity_zero/blob/main/docs/architecture/asmdef-graph.md)
- **Services**: 16 modules under [`docs/services/`](https://github.com/tnbao91/unity_zero/tree/main/docs/services)
- **UI**: [`docs/ui/`](https://github.com/tnbao91/unity_zero/tree/main/docs/ui) — popup-stack, safe-area, loading-screen, toast, localized-text, ui-root
- **Gameplay**: [`docs/gameplay/`](https://github.com/tnbao91/unity_zero/tree/main/docs/gameplay) — state-machine, level-loading
- **Live-Ops**: [`docs/liveops/`](https://github.com/tnbao91/unity_zero/tree/main/docs/liveops) — version-check, addressables-remote
- **Security**: [`docs/security/`](https://github.com/tnbao91/unity_zero/tree/main/docs/security) — save-encryption
- **Testing**: [`docs/testing/`](https://github.com/tnbao91/unity_zero/tree/main/docs/testing) — writing-tests, ci, manual-checklist
- **Dev tools**: [`docs/dev/`](https://github.com/tnbao91/unity_zero/tree/main/docs/dev) — cheat-console, fps-overlay
- **Meta recipes** (per-game patterns): [`docs/meta/recipes.md`](https://github.com/tnbao91/unity_zero/blob/main/docs/meta/recipes.md)
