#!/usr/bin/env bash
# Fails when any project in the solution uses a NuGet package with a known security advisory,
# directly or through another package. Run locally before a commit, or let CI run it
# (.github/workflows/vulnerable-packages.yml). A failure to reach the package source also fails,
# so the check never passes without having looked.
set -euo pipefail

cd "$(dirname "$0")/.."

dotnet restore BloodBankLIS.slnx --verbosity quiet
report="$(dotnet list BloodBankLIS.slnx package --vulnerable --include-transitive 2>&1)"
echo "$report"

if grep -q "has the following vulnerable packages" <<<"$report"; then
  echo "::error::Vulnerable NuGet packages found. Update them (prefer the parent package) or pin a patched version, then rerun this script."
  exit 1
fi

echo "No vulnerable NuGet packages."
