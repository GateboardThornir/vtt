#!/usr/bin/env bash
# Runs the server against the local compose stack. .NET does not read .env, so this script is what
# turns the file into process environment: it is the single place the development credentials are
# assembled into a connection string.
set -euo pipefail

repo_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
cd "$repo_root"

if [[ ! -f .env ]]; then
  echo "No .env found in $repo_root — copy .env.example to .env and set a password." >&2
  exit 1
fi

# "set -a" exports every variable assigned until it is turned off again.
set -a
# shellcheck disable=SC1091
. ./.env
set +a

# Double underscore is how the .NET configuration provider spells a nested key: this becomes
# ConnectionStrings:Default.
export ConnectionStrings__Default="Host=localhost;Port=${POSTGRES_HOST_PORT:-55432};Database=${POSTGRES_DB};Username=${POSTGRES_USER};Password=${POSTGRES_PASSWORD}"

exec dotnet run --project src/Server "$@"
