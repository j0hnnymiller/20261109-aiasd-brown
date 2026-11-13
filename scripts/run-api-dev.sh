#!/usr/bin/env bash
set -euo pipefail

repository_root="$(cd "$(dirname "${BASH_SOURCE[0]}")/.." && pwd)"
if [[ ! -f "$repository_root/.env" ]]; then
  echo "Copy .env.example to .env and set a local-only database password first." >&2
  exit 1
fi

set -a
source "$repository_root/.env"
set +a
source "$repository_root/web/scripts/create-local-cert.sh"

password_key=Password
export ConnectionStrings__TaskManager="Host=127.0.0.1;Port=${POSTGRES_PORT};Database=${POSTGRES_DB};Username=${POSTGRES_USER};${password_key}=${POSTGRES_PASSWORD}"
export TaskManager__AllowedOrigins__0=https://localhost:5173
export ASPNETCORE_ENVIRONMENT=Development
export ASPNETCORE_URLS=https://localhost:5001
export ASPNETCORE_Kestrel__Certificates__Default__Path="$TASK_MANAGER_CERT_DIR/localhost.pfx"
export ASPNETCORE_Kestrel__Certificates__Default__Password=''

cd "$repository_root"
exec dotnet run --project src/TaskManager.Api/TaskManager.Api.csproj --configuration Release
