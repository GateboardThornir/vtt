#!/usr/bin/env bash
# Runs `dotnet ef` against the local compose stack.
#
# This is not a convenience wrapper. `dotnet ef` builds the server and then executes Program.cs up
# to builder.Build(), where it intercepts the host and takes the service provider — which means the
# fail-fast check in DatabaseConnectionString.Resolve runs at design time too, and without the
# environment it throws. Supplying the same environment the server gets is what makes the EF tools
# work at all, and it keeps one answer to "where does the connection string come from".
#
#   scripts/ef.sh migrations add SomeName --output-dir Infrastructure/Migrations
#   scripts/ef.sh migrations remove
#   scripts/ef.sh database update
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [[ ! -f .env ]]; then
  echo "No .env found in $repo_root — copy .env.example to .env and set a password." >&2
  exit 1
fi

set -a
# shellcheck disable=SC1091
. ./.env
set +a

export ConnectionStrings__Default="Host=localhost;Port=${POSTGRES_HOST_PORT:-55432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

# dotnet-ef is pinned in .config/dotnet-tools.json rather than installed globally, so its version
# travels with the repository instead of living in whoever's machine.
dotnet tool restore >/dev/null

exec dotnet ef "$@" --project src/Server
