#!/usr/bin/env bash
# Consumer-mirror drift guard for the AI harness.
#
# The maintainer-side harness (.claude/agents + .claude/commands) has a
# game-tuned counterpart shipped to consumers in the ClaudeMemory sample.
# The two sides are NOT byte-identical by design (consumer files are tuned
# for game-on-Zero scope), so this guard does not diff content. It catches
# the real failure mode: a maintainer-side file changes in a PR and its
# consumer counterpart is not touched at all — new footguns / workflow
# changes silently never reach consumers.
#
# Plain "maintainer|consumer" pair list (no associative arrays) so the
# script also runs under macOS's stock bash 3.2 for local checks.
#
# Usage: check-claude-mirror.sh <base-ref>   (e.g. origin/main)
set -euo pipefail

BASE_REF="${1:?usage: check-claude-mirror.sh <base-ref>}"
MIRROR_ROOT="Packages/com.tnbao91.nobody.zero/Samples~/ClaudeMemory/.claude"

# maintainer path | consumer counterpart (tier agents are renamed unity-* → game-*)
PAIRS=(
  ".claude/agents/asmdef-boundary-reviewer.md|$MIRROR_ROOT/agents/asmdef-boundary-reviewer.md"
  ".claude/agents/pitfalls-guard.md|$MIRROR_ROOT/agents/pitfalls-guard.md"
  ".claude/agents/service-scaffolder.md|$MIRROR_ROOT/agents/service-scaffolder.md"
  ".claude/agents/unity-lead.md|$MIRROR_ROOT/agents/game-lead.md"
  ".claude/agents/unity-senior.md|$MIRROR_ROOT/agents/game-senior.md"
  ".claude/agents/unity-junior.md|$MIRROR_ROOT/agents/game-junior.md"
  ".claude/commands/phase-open.md|$MIRROR_ROOT/commands/phase-open.md"
  ".claude/commands/phase-close.md|$MIRROR_ROOT/commands/phase-close.md"
  ".claude/commands/pre-pr.md|$MIRROR_ROOT/commands/pre-pr.md"
)

status=0
changed="$(git diff --name-only "$BASE_REF"...HEAD)"

for pair in "${PAIRS[@]}"; do
  m="${pair%%|*}"
  c="${pair##*|}"

  # 1) Presence — every mapped file must exist on both sides.
  if [ ! -f "$m" ]; then
    echo "::error::maintainer harness file missing: $m"
    status=1
  fi
  if [ ! -f "$c" ]; then
    echo "::error::consumer mirror missing: $c (counterpart of $m)"
    status=1
  fi

  # 2) Drift — a maintainer-side file changed without its mirror changing.
  if grep -qxF "$m" <<<"$changed" && ! grep -qxF "$c" <<<"$changed"; then
    echo "::error file=$m::changed without its consumer mirror ($c) — port the change to the game-tuned counterpart (or update it with a 'not applicable to consumers' note)"
    status=1
  fi
done

if [ "$status" -eq 0 ]; then
  echo "claude-mirror: OK (presence + drift)"
fi
exit "$status"
