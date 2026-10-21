#!/usr/bin/env bash
# Runs the backend test suite: build the solution once (serialized — parallel MSBuild
# is broken in this .NET 10 environment, see commands.md), then run the 4 test
# projects as independent OS processes in parallel. Each `dotnet test` call still
# passes -m:1, so no single process attempts the parallel-build path that's broken —
# only the OS scheduler runs the 4 already-built processes concurrently.
#
# Measured effect (479 tests, 2026-08-09): 55.8s serial (`dotnet test
# backend/FeedbackHub.sln --no-restore -m:1`) -> 29.7s with this script.
#
# Tests tagged [Trait("Category", "RealData")] read gitignored real source data (an import file,
# a large reference data set) and are skipped by default — they only change outcome when that data
# changes. Set FEEDBACKHUB_TEST_REAL_DATA=1 to include them.
#
# FEEDBACKHUB_TEST_COVERAGE=1 also collects coverage (coverlet, Cobertura) into
# backend/TestResults/coverage/ — one coverage.cobertura.xml per test project; CI merges them into
# the run summary with ReportGenerator (see .claude/rules/testing.md § Coverage).
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")/../.." && pwd)"
cd "$ROOT_DIR"

dotnet build backend/FeedbackHub.sln --no-restore -m:1

projects=(
  backend/tests/FeedbackHub.Domain.Tests/FeedbackHub.Domain.Tests.csproj
  backend/tests/FeedbackHub.Application.Tests/FeedbackHub.Application.Tests.csproj
  backend/tests/FeedbackHub.Infrastructure.Tests/FeedbackHub.Infrastructure.Tests.csproj
  backend/tests/FeedbackHub.API.Tests/FeedbackHub.API.Tests.csproj
)

filter_args=()
if [[ "${FEEDBACKHUB_TEST_REAL_DATA:-0}" != "1" ]]; then
  filter_args=(--filter "Category!=RealData")
fi

coverage_args=()
if [[ "${FEEDBACKHUB_TEST_COVERAGE:-0}" == "1" ]]; then
  coverage_dir="$ROOT_DIR/backend/TestResults/coverage"
  rm -rf "$coverage_dir"
  coverage_args=(--collect "XPlat Code Coverage" --results-directory "$coverage_dir")
fi

pids=()
for project in "${projects[@]}"; do
  # ${a[@]+...}: under set -u, bash before 4.4 (macOS ships 3.2) calls an empty array unbound.
  dotnet test "$project" --no-build --no-restore -m:1 ${filter_args[@]+"${filter_args[@]}"} \
    ${coverage_args[@]+"${coverage_args[@]}"} &
  pids+=("$!")
done

status=0
for pid in "${pids[@]}"; do
  wait "$pid" || status=1
done

exit "$status"
