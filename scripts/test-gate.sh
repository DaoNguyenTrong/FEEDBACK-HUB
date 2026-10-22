#!/usr/bin/env bash
# Runs locally the same checks as CI's test gate (.github/workflows/test.yml), minus coverage — the
# gate /git-release runs before it merges and tags (.claude/release-kit.json), and the one to run
# before pushing. Change it together with test.yml so a release can't pass here and fail on the tag.
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$ROOT_DIR"

# One EXIT trap for every temp file; INT/TERM exit through it, so Ctrl-C cleans up too.
temp_files=()
cleanup() { if ((${#temp_files[@]})); then rm -f -- "${temp_files[@]}"; fi; }
trap cleanup EXIT
trap 'exit 130' INT
trap 'exit 143' TERM

# CI copies the example config before the backend jobs; the OpenAPI export dies without it
# (CorsSettings). An existing appsettings.json is per-machine — never touched, only a missing one
# gets a copy, removed on exit.
appsettings=backend/src/FeedbackHub.API/appsettings.json
if [[ ! -e "$appsettings" && ! -L "$appsettings" ]]; then
  echo "test-gate: $appsettings missing — using a temporary copy of appsettings.Example.json."
  temp_files+=("$appsettings")
  cp backend/src/FeedbackHub.API/appsettings.Example.json "$appsettings"
fi

# Comment length (.claude/rules/comments.md) — seconds, so it fails fast.
bun test scripts/
bun scripts/check-comments.ts

backend/scripts/test.sh

# A controller/DTO change without a re-export leaves the frontend's generated client stale. Compared
# against the file as it was before the export, not against git (CI's check), so an export that is
# re-run but not committed yet still passes locally.
spec=shared/openapi/openapi.json
before="$(mktemp)"
temp_files+=("$before")
cp "$spec" "$before"
dotnet build backend/src/FeedbackHub.API/FeedbackHub.API.csproj --no-restore -m:1 -p:OpenApiGenerateDocumentsOnBuild=true
if ! cmp -s "$before" "$spec"; then
  echo "$spec was out of date and has been re-exported — review and commit it (commands.md § OpenAPI Spec Export)." >&2
  exit 1
fi

bun run --cwd frontend prettier --check --experimental-cli src/ e2e/
# src/typings/ is written by the Vite plugins, complete only after a build — a fresh clone or
# worktree has none, and vue-tsc fails without it.
bun run --cwd frontend build-only
bun run --cwd frontend type-check
bun run --cwd frontend lint:boundaries
bun run --cwd frontend test:run

# The user guide: formatting, unit + content integrity tests, and a build that fails on dead links.
bun install --cwd frontend/user-guide/site --frozen-lockfile
bun run --cwd frontend/user-guide/site format:check
bun run --cwd frontend/user-guide/site test
bun run --cwd frontend/user-guide/site build
