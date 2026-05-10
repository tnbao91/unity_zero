## Summary

<!-- 1-3 bullets on what changed and why. -->

## Type

- [ ] Bug fix
- [ ] Feature
- [ ] Refactor / cleanup
- [ ] Docs only
- [ ] Test only
- [ ] Chore (deps, CI, repo)

## Verification

- [ ] Editor compile clean (no console errors).
- [ ] Test Runner EditMode green (`Window → General → Test Runner`).
- [ ] If touching Bootstrap pipeline / installers: `Bootstrap.unity` Press Play runs all 16 steps without exception.
- [ ] If touching public surface (interfaces, installer signatures, package layout): updated relevant doc under `docs/`.
- [ ] If new convention or pitfall: added to `CLAUDE.md` and/or `docs/dev/PITFALLS.md`.
- [ ] If touching `Samples~/BootstrapScene/` canonical assets: synced from dev workspace per `CONTRIBUTING.md`.

## Risk

<!-- Anything reviewers should pay extra attention to: cross-asmdef impact, reflection-based discovery, lifecycle order, ifdef-gated code, etc. -->

## Linked issues

Closes #
