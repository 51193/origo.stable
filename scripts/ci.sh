#!/usr/bin/env bash
# Origo 标准 CI：与 GitHub Actions 使用相同步骤（restore → build → test，含 Coverlet 行覆盖率门禁）。
set -euo pipefail
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
cd "$ROOT"

print_coverage_banner() {
  echo ""
  echo "══════════════════════════════════════════════════════════════════════"
  echo " Origo.Core LINE COVERAGE GATE (enforced in CI and local runs)"
  echo " ────────────────────────────────────────────────────────────────────"
  echo " • Tool: Coverlet (coverlet.msbuild) on project Origo.Core.Tests"
  echo " • Rule: total LINE coverage for module Origo.Core must be >= 90%"
  echo " • Excluded from coverage % denominator: Origo.Core/Runtime/OrigoAutoInitializer.cs"
  echo " • If coverage is below 90%, 'dotnet test' fails with a Coverlet error (non-zero exit)."
  echo " • Below, after tests, Coverlet prints a summary table (Line / Branch / Method)."
  echo "══════════════════════════════════════════════════════════════════════"
  echo ""
}

print_coverage_banner

if [[ -n "${GITHUB_ACTIONS:-}" ]]; then
  echo "::notice title=Origo.Core line coverage::Coverlet enforces total line coverage >= 90% for Origo.Core (Origo.Core.Tests). Summary table is printed after tests."
fi

dotnet restore Origo.sln
dotnet build Origo.sln --no-restore --configuration Release

echo ""
echo ">>> Running tests — Coverlet will fail the job if Origo.Core LINE coverage is below 90%."
echo ""

dotnet test Origo.sln --no-build --configuration Release --verbosity normal
