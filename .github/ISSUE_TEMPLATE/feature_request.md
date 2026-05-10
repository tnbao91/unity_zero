---
name: Feature request
about: Suggest an addition or enhancement
title: "[feature] "
labels: enhancement
---

## Problem

<!-- What pain point does this solve? Avoid solution-first framing. -->

## Proposed approach

<!-- Sketch the change. If it touches the architecture (new asmdef, cross-peer ref, breaking interface change), say so explicitly. -->

## Scope check

- [ ] Fits the template's "minimal infra, opinion-free meta" philosophy (see `docs/dev/PLAN.md` §1).
- [ ] Does NOT introduce genre-specific gameplay (grid, runner, idle, merge, match-3) — those belong in consumer game code.
- [ ] Does NOT break the peer rule (Gameplay/UI/Meta cannot reference each other).
- [ ] Does NOT add a real third-party SDK to the template (Mock + extension recipe pattern only — Localization and Notifications are the documented exceptions).

## Alternatives considered

<!-- Optional but appreciated. -->
