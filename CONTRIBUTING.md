# Contributing

Unity Zero is an opensource game template. Contributions land via PRs against `main`. Codex / Opus reviewers spot-check; tone-of-voice in commit messages, journal entries, and docs is detailed and defensible — vague descriptions get pushed back.

## Source of truth

- **`docs/dev/PLAN.md`** — architectural decisions, phase plan, negative scope. Read before any non-trivial change.
- **`docs/dev/JOURNAL.md`** — append-only history of what landed each phase. New phases append a fresh entry.
- **`docs/dev/PITFALLS.md`** — every entry came from a real bug. Add to it when a new footgun surfaces.
- **`CLAUDE.md`** — index of conventions and "easy to miss" surprises. Update before merging anything that changes a public surface.

If `PLAN.md` and the code drift, the code is wrong — not the plan. Reconcile by either editing the code or, if the design genuinely changed, amending `PLAN.md` in the same PR with a one-line note in the journal.

## Adding a new service

The "service convention" in `CLAUDE.md` is the recipe. Concretely:

1. **Interface** at `Assets/_Project/Scripts/Runtime/Core/Interfaces/I<Name>Service.cs`. Namespace `Zero.Core`. Methods returning `UniTask` for async, `Observable<T>` (R3) for streams.
2. **Implementation folder** at `Assets/_Project/Scripts/Runtime/Services/<Name>/` with its own `Zero.Services.<Name>.asmdef`. Mirror an existing asmdef (`Zero.Services.Audio.asmdef` is a clean reference) — `autoReferenced: false`, references listed by **string name** not GUID, only the deps you actually use.
3. **Installer** `<Name>ServiceInstaller.cs` — static class with `Install(ContainerBuilder builder)`. Almost always one `RegisterType` call: `builder.RegisterType(typeof(<Impl>), new[] { typeof(I<Name>Service) }, Lifetime.Singleton, Resolution.Lazy);`. If the ctor takes a non-contract value (string, int, computed), use `RegisterFactory` — see `VersionCheckServiceInstaller.cs` for the canonical example.
4. **Bootstrap step** (only if init must happen at startup) — `<Name>Step : BootstrapStepBase` at `Assets/_Project/Scripts/Runtime/Bootstrap/Steps/<Name>Step.cs`. Override `Name`, `IsCritical` (default false; only Crashlytics is currently critical), and optionally `Timeout` / `MaxRetries`.
5. **Wire** in `ProjectScopeInstaller.cs`: add `<Name>ServiceInstaller.Install(builder)` and the step to the `steps` array in the right position. Place steps that need save data after `SaveStep`, steps that need remote config after `RemoteConfigStep`.
6. **Reference** the new asmdef from `Zero.Bootstrap.asmdef` so the composition root can see it.
7. **Tests** at `Assets/_Project/Scripts/Tests/EditMode/<Name>ServiceTests.cs`. Reference the new asmdef from `Zero.Tests.EditMode.asmdef`.
8. **Doc** at `docs/services/<name>.md` matching the fixed format (Overview / Public API / Extension Points / Examples / Known Limitations / Design Rationale).

## Extending Mock SDKs

The template ships mock implementations for Crashlytics, Consent, RemoteConfig, Analytics, Attribution, Ads, IAP, and ReceiptValidator. Each has a per-mock recipe in `docs/services/<name>.md` showing exactly which installer line to swap. The pattern is always:

1. Add the SDK package via OpenUPM, NuGet, or Unity Package Manager.
2. Add the SDK's asmdef to your service's `Zero.Services.<Name>.asmdef` references.
3. Write `<Vendor><Name>Service : I<Name>Service` in the service folder.
4. Replace the `RegisterType(typeof(Mock<Name>Service), ...)` call in `<Name>ServiceInstaller.cs` with the new impl.
5. Mock impls stay in the repo and are still selectable via the `ZERO_USE_MOCK_<NAME>` define for headless testing.

## Test conventions

- **EditMode** tests live under `Assets/_Project/Scripts/Tests/EditMode/`. PlayMode tests under `PlayMode/`. Both gated on `UNITY_INCLUDE_TESTS`.
- **Async EditMode tests** use `[UnityTest] public IEnumerator Foo() => UniTask.ToCoroutine(async () => { ... })`. NUnit's `[Test]` does not await `UniTask`. Pure-sync tests can keep `[Test]`.
- **Tests that subscribe via lambda** must `using R3;` — `Observable<T>.Subscribe(Action<T>)` is an extension method.
- **Tests with `[UnityTest]`** must `using UnityEngine.TestTools;`.
- **Stubs** that implement repo interfaces should be **`private nested`** inside the test class to avoid namespace-level duplicate-type collisions across multiple test files.
- See `docs/testing/writing-tests.md` for full pattern.

## Commit messages

Conventional commits: `feat(<scope>)`, `fix(<scope>)`, `refactor(<scope>)`, `test(<scope>)`, `docs(<scope>)`, `chore(<scope>)`. The scope matches the asmdef stem (`gameplay`, `ui`, `versioncheck`, `devtools`, `audio`, etc.). Multi-line bodies explain *why*, not what — the diff already shows what.

## Sample sync convention

The package ships a `Samples~/BootstrapScene/` bundle that mirrors the dev workspace's canonical assets. Because `Samples~/` ends in `~`, Unity ignores it during dev (so the dev project keeps its own `Assets/_Project/Scenes/Bootstrap.unity` and `Assets/Resources/{ReflexSettings,ZeroSecrets.asset.example}.asset`).

When you change any of the canonical assets, copy them to `Packages/com.tnbao91.nobody.zero/Samples~/BootstrapScene/` **before tagging a release**. Files to keep in sync:

- `Assets/_Project/Scenes/Bootstrap.unity` (+ `.meta`)
- `Assets/Resources/ZeroSecrets.asset.example` (+ `.meta`)
- `Assets/Resources/ReflexSettings.asset` (+ `.meta`)
- `Assets/packages.config`

Forgetting to sync means consumers who Import the sample get a stale Bootstrap scene. A pre-tag lint script can be added in a future phase.

## Phase workflow

Larger work is staged as phases per `docs/dev/PLAN.md`. The flow is:

1. Branch `phase-<N>-<short-name>` off `main`.
2. Implement (often via subagent + lead review). Commit incrementally.
3. Verify in Editor — compile clean, Test Runner green, Press Play `Bootstrap.unity` runs through.
4. Append a `JOURNAL.md` entry. Update `CLAUDE.md` if any public surface or convention changed.
5. Merge `--no-ff` to `main`. The merge commit message summarizes the phase.

## Code review

Codex reviews PRs after merge and comments on the journal entries / public docs. Defensible design rationale matters more than passing tests; the journal records *why* something landed, not just that it landed. When in doubt about a tradeoff, document the alternatives you considered and which constraint broke the tie.
