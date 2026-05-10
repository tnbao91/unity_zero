# Zero — Hybrid Casual Template (UPM)

Opinionated Unity 6 LTS template for hybrid casual and puzzle games. Stack: Reflex DI · UniTask · R3 · LitMotion · Addressables · Newtonsoft · ZString · New Input System. ~28 services with mock-first defaults so the template runs end-to-end on a fresh clone; swap mocks per-game in installer files.

> **Meta layer (wallet / progression / rewards) is intentionally out of scope** — hybrid casual and puzzle have different meta loops. See [`docs/meta/recipes.md`](https://github.com/tnbao91/unity_zero/tree/main/docs/meta/recipes.md) for per-game patterns.

## Quick Start

### 1. Add scoped registries

In `Packages/manifest.json`:

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

### 2. Install NuGetForUnity (prereq for R3's BCL transitive deps)

Package Manager → "+" → Add package by name → `com.github-glitchenzo.nugetforunity`.

### 3. Install Zero

Package Manager → "+" → Add package by name → `com.tnbao91.nobody.zero`. Or via `openupm-cli`:

```sh
openupm add com.tnbao91.nobody.zero
```

### 4. Import Bootstrap sample

Package Manager → select **Zero — Hybrid Casual Template** → Samples → click **Import** next to "Bootstrap Scene". Files copy to `Assets/Samples/com.tnbao91.nobody.zero/<version>/BootstrapScene/`.

Then:
- Move `Bootstrap.unity` to wherever you keep scenes; add to Build Settings.
- Move `ReflexSettings.asset` to `Assets/Resources/ReflexSettings.asset`.
- Copy `packages.config` to `Assets/packages.config`. Open NuGet menu → **Restore Packages** to fetch the 4 BCL DLLs R3 needs.

### 5. Configure save encryption seeds

- Copy `ZeroSecrets.asset.example` to `Assets/Resources/ZeroSecrets.asset`.
- Open in Inspector. Replace the placeholder seeds (`REPLACE_ME_*`) with per-game random strings — these encrypt save data; do **not** commit them.

> Player builds throw `InvalidOperationException` at startup if seeds remain at their placeholder values. Editor warns loudly but continues so iteration isn't blocked.

### 6. Press Play

Open Bootstrap.unity → Press Play. Console logs `[Bootstrap] Step N/16: ...` for each pipeline step.

## AI agent compatibility

This template is designed for AI-agent-assisted development. If you use Claude Code (or similar coding agents), import the **Claude Memory** sample alongside Bootstrap Scene:

`Package Manager → Zero → Samples → Import "Claude Memory"`.

Move `CLAUDE.md` + `claude-context/` from `Assets/Samples/com.tnbao91.nobody.zero/<version>/ClaudeMemory/` to your repo root. Claude Code will auto-read them at session start, giving it: stack constraints (locked Reflex/UniTask/R3/LitMotion), architecture cheatsheet, ~28 service interfaces, extension recipes, and consumer-relevant pitfalls. Saves the agent from re-deriving conventions every conversation.

Full readme inside the sample bundle.

## Documentation

Full docs at the repo: <https://github.com/tnbao91/unity_zero/tree/main/docs>

- Architecture: [event-bus](https://github.com/tnbao91/unity_zero/blob/main/docs/architecture/event-bus.md), [bootstrap-pipeline](https://github.com/tnbao91/unity_zero/blob/main/docs/architecture/bootstrap-pipeline.md), [asmdef-graph](https://github.com/tnbao91/unity_zero/blob/main/docs/architecture/asmdef-graph.md)
- Services: 16 modules under `docs/services/`
- UI / Gameplay / Live-Ops / Dev tools: under their respective folders
- Mock SDK extension recipes: 8 SDKs under `docs/services/` (Crashlytics, Consent, RemoteConfig, Analytics, Attribution, Ads, IAP, ReceiptValidator)

## License

MIT — see [LICENSE.md](LICENSE.md).
