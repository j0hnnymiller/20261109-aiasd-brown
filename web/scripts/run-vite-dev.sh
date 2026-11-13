#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$script_dir/create-local-cert.sh"
export VITE_USE_HTTPS=true
export TASK_MANAGER_API_TARGET="${TASK_MANAGER_API_TARGET:-https://localhost:5001}"

exec "$script_dir/../node_modules/.bin/vite" --host localhost --port 5173 --strictPort
