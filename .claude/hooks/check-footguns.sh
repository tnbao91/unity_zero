#!/usr/bin/env bash
# check-footguns.sh — PostToolUse(Edit|Write) guardrail for the Unity Zero template.
#
# Catches the *context-free* subset of docs/dev/PITFALLS.md — single-file,
# regex-decidable footguns that a path-based permission cannot see inside file
# content. Judgment checks (RegisterType-with-primitive, Addressables
# HasKeyAsync pre-check, asmdef peer/transitive nuance) stay in the
# `pitfalls-guard` / `asmdef-boundary-reviewer` agents — do NOT copy them here.
#
# WARN-ONLY by design: it never blocks (always exits 0). Findings are surfaced
# to the model via hookSpecificOutput.additionalContext on STDOUT and to the
# human via systemMessage. (A PostToolUse hook's stderr on exit 0 is NOT shown
# by the harness — "silent success is invisible by design" — so warnings must
# go through this JSON channel, not stderr.)
#
# Input: PostToolUse hook JSON on stdin. We read tool_input.file_path.

set -u

# --- extract the edited file path from the hook payload -----------------------
file="$(python3 -c '
import sys, json
try:
    d = json.load(sys.stdin)
except Exception:
    sys.exit(0)
ti = d.get("tool_input", {}) or {}
print(ti.get("file_path", "") or "")
' 2>/dev/null)"

# Only inspect C# source files that still exist.
case "$file" in
  *.cs) ;;
  *) exit 0 ;;
esac
[ -f "$file" ] || exit 0

findings=""
add() { findings="${findings}• $1\n"; }

# --- B. legacy Input.* API (Input System is the active handler) ---------------
if grep -nE 'UnityEngine\.Input\.|(^|[^.[:alnum:]])Input\.(GetKey|GetKeyDown|GetKeyUp|GetButton|touchCount|GetTouch|mousePosition|mouseScrollDelta|GetMouseButton|acceleration)' "$file" >/dev/null; then
  add "legacy Input.* API — Active Input Handling is 'Input System Package', legacy calls throw at runtime. Use Keyboard.current / Mouse.current / Touchscreen.current or IInputService."
fi

# --- J. dynamic in runtime code (IL2CPP/AOT has no DLR) -----------------------
# Strip // line comments first to avoid flagging prose; best-effort.
if sed 's://.*$::' "$file" | grep -nE '(^|[^.[:alnum:]])dynamic[[:space:]]' >/dev/null; then
  add "'dynamic' keyword — IL2CPP/AOT does not support the DLR; fails at link time on iOS/Android/WebGL. Use a non-generic interface + explicit cast."
fi

# --- C# 10+ syntax (Unity 6 = C# 9) -------------------------------------------
if grep -nE '\brecord[[:space:]]+struct\b|\binit[[:space:]]*;|\brequired[[:space:]]+(public|internal|private|protected|readonly|[A-Za-z])|^namespace[[:space:]]+[A-Za-z0-9_.]+[[:space:]]*;' "$file" >/dev/null; then
  add "C# 10+ syntax — Unity 6 is C# 9: no 'record struct', 'init;', 'required' members, or file-scoped namespaces."
fi

# --- H. R3 Subscribe lambda without 'using R3;' (CS1660 wrong overload) -------
if grep -nE '\.Subscribe[[:space:]]*\(' "$file" >/dev/null && ! grep -nE '^[[:space:]]*using[[:space:]]+R3[[:space:]]*;' "$file" >/dev/null; then
  add "Subscribe(...) without 'using R3;' — Subscribe(Action<T>) is an R3 extension method; without the using the lambda binds to Subscribe(Observer<T>) and emits CS1660."
fi

# Nothing to report → stay silent, exit clean.
[ -z "$findings" ] && exit 0

# Emit findings via the documented PostToolUse JSON channel (stdout). Never block.
msg="$(printf '⚠ Unity Zero footgun(s) in %s:\n%b' "$file" "$findings")"
python3 -c '
import json, sys
msg = sys.argv[1]
print(json.dumps({
    "systemMessage": msg,
    "hookSpecificOutput": {
        "hookEventName": "PostToolUse",
        "additionalContext": msg + "\n(warn-only — see docs/dev/PITFALLS.md \"Enforcement surface\"; pitfalls-guard re-checks on the full diff.)"
    }
}))
' "$msg"
exit 0
