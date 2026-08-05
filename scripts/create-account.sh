#!/usr/bin/env bash
# Creates an account directly, bypassing the invite requirement — see ADR 008.
#
# Needed once on a fresh database, to break the circle where registration requires an invite and an
# invite requires an account to have issued it. The password is prompted for; do not try to pass it
# as an argument, because arguments are visible in the process list and stay in shell history.
#
#   scripts/create-account.sh mattia
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

exec dotnet run --project src/Server -- create-account "$@"
