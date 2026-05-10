# Changelog — `com.tnbao91.nobody.zero`

Format: [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) loosely; per-phase implementation deltas live in [`docs/dev/JOURNAL.md`](https://github.com/tnbao91/unity_zero/blob/main/docs/dev/JOURNAL.md) at the repo.

## [0.2.1] — 2026-05-10

Hotfix: restore `Runtime/Services/Log/` folder (6 files) lost during the v0.1.0 UPM restructure due to a global gitignore `log` pattern collision on case-insensitive macOS. v0.1.0 and v0.2.0 ship with the broken state and should NOT be installed; v0.2.1 is the first installable release. `.gitignore` now negates the path to prevent recurrence.

## [0.2.0] — 2026-05-10

Adds `Samples~/ClaudeMemory/` AI agent context bundle for Claude Code-assisted consumer development.

### Added
- `Samples~/ClaudeMemory/` — `CLAUDE.md` + `claude-context/` (architecture, available-services, extension-points, stack-constraints, pitfalls) + `.claude/settings.example.json`.
- `package.json` registers the new sample.
- README section advertises AI agent compatibility.

### Notes
- Consumer imports the sample, moves `CLAUDE.md` + `claude-context/` to their repo root. Claude Code auto-reads them at session start.
- Claude-only scope. Cross-tool agent docs (`AGENTS.md`, Cursor rules, Copilot instructions) deferred.

## [0.1.0] — 2026-05-10

Initial release as Unity Package Manager package. Restructured from full-project template to embedded-package layout for `git+https://...?path=Packages/...` install + OpenUPM publish. All v0 template features carried over from the prior project-based release.

### Added
- `package.json` declaring 11 OpenUPM/Unity dependencies (UniTask, R3, ZString, Reflex, LitMotion, Addressables, Localization, Mobile Notifications, Newtonsoft, Purchasing, Input System).
- `Samples~/BootstrapScene/` bundle: Bootstrap.unity, ZeroSecrets.asset.example, ReflexSettings.asset, packages.config (consumer-side NuGet restore).
- 5-step consumer Quick Start in package README.

### Notes
- BCL transitive deps for R3 (`Microsoft.Bcl.AsyncInterfaces`, `Microsoft.Bcl.TimeProvider`, `System.ComponentModel.Annotations`, `System.Threading.Channels`) require NuGetForUnity prereq; not bundled to avoid duplicate-DLL conflicts when consumer follows R3's OpenUPM README.
- Documentation lives at [GitHub `docs/`](https://github.com/tnbao91/unity_zero/tree/main/docs); package tarball does not include offline docs in v0.1.0.
