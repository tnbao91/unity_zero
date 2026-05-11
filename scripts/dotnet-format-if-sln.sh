#!/usr/bin/env bash
# Runs `dotnet format whitespace --verify-no-changes` only if Unity has
# generated a *.sln. On fresh clone the sln does not exist; skip cleanly
# instead of failing.
set -euo pipefail

if ! ls *.sln >/dev/null 2>&1; then
    echo "skip: no .sln in repo root (open Unity once to regenerate)"
    exit 0
fi

if ! command -v dotnet >/dev/null 2>&1; then
    echo "skip: dotnet CLI not on PATH"
    exit 0
fi

dotnet format whitespace --verify-no-changes --no-restore
