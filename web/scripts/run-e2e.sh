#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
source "$script_dir/create-local-cert.sh"
export TASK_MANAGER_CERT_DIR
export NODE_EXTRA_CA_CERTS

exec node "$script_dir/run-e2e.mjs" "$@"
