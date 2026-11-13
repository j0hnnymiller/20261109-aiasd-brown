#!/usr/bin/env bash
set -euo pipefail

script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cert_dir="${TASK_MANAGER_CERT_DIR:-"$(cd "$script_dir/.." && pwd)/.certs"}"
mkdir -p "$cert_dir"

if [[ ! -s "$cert_dir/root-ca.crt" || ! -s "$cert_dir/localhost.crt" || ! -s "$cert_dir/localhost.pfx" ]]; then
  rm -f "$cert_dir"/root-ca.* "$cert_dir"/localhost.*
  openssl genrsa -out "$cert_dir/root-ca.key" 3072 >/dev/null 2>&1
  openssl req -x509 -new -sha256 -key "$cert_dir/root-ca.key" \
    -out "$cert_dir/root-ca.crt" -days 3650 \
    -subj "/CN=Task Manager Local Test CA" \
    -addext "basicConstraints=critical,CA:TRUE" \
    -addext "keyUsage=critical,keyCertSign,cRLSign" >/dev/null 2>&1
  openssl req -new -newkey rsa:2048 -nodes \
    -keyout "$cert_dir/localhost.key" \
    -out "$cert_dir/localhost.csr" \
    -subj "/CN=localhost" >/dev/null 2>&1
  cat > "$cert_dir/localhost.ext" <<'EOF'
basicConstraints=critical,CA:FALSE
keyUsage=critical,digitalSignature,keyEncipherment
extendedKeyUsage=serverAuth
subjectAltName=DNS:localhost,IP:127.0.0.1
EOF
  openssl x509 -req -sha256 -in "$cert_dir/localhost.csr" \
    -CA "$cert_dir/root-ca.crt" -CAkey "$cert_dir/root-ca.key" -CAcreateserial \
    -out "$cert_dir/localhost.crt" -days 825 -extfile "$cert_dir/localhost.ext" >/dev/null 2>&1
  openssl pkcs12 -export -out "$cert_dir/localhost.pfx" \
    -inkey "$cert_dir/localhost.key" -in "$cert_dir/localhost.crt" \
    -certfile "$cert_dir/root-ca.crt" -passout pass: >/dev/null 2>&1
  chmod 600 "$cert_dir/root-ca.key" "$cert_dir/localhost.key" "$cert_dir/localhost.pfx"
  chmod 644 "$cert_dir/root-ca.crt" "$cert_dir/localhost.crt"
fi

openssl verify -CAfile "$cert_dir/root-ca.crt" "$cert_dir/localhost.crt" >/dev/null
openssl x509 -in "$cert_dir/localhost.crt" -noout -checkhost localhost >/dev/null
openssl x509 -in "$cert_dir/localhost.crt" -noout -checkip 127.0.0.1 >/dev/null

if command -v update-ca-certificates >/dev/null 2>&1; then
  if [[ "$(id -u)" == "0" ]]; then
    install -m 644 "$cert_dir/root-ca.crt" /usr/local/share/ca-certificates/task-manager-local-ca.crt
    update-ca-certificates >/dev/null
  elif command -v sudo >/dev/null 2>&1 && sudo -n true 2>/dev/null; then
    sudo install -m 644 "$cert_dir/root-ca.crt" /usr/local/share/ca-certificates/task-manager-local-ca.crt
    sudo update-ca-certificates >/dev/null
  fi
fi

if ! command -v certutil >/dev/null 2>&1; then
  echo "Install libnss3-tools (certutil) to trust the local HTTPS test certificate." >&2
  exit 1
fi

nss_database="$HOME/.pki/nssdb"
mkdir -p "$nss_database"
if ! certutil -L -d "sql:$nss_database" >/dev/null 2>&1; then
  certutil -N --empty-password -d "sql:$nss_database" >/dev/null 2>&1
fi
certutil -D -d "sql:$nss_database" -n TaskManagerLocalCA >/dev/null 2>&1 || true
certutil -A -d "sql:$nss_database" -n TaskManagerLocalCA -t "C,," \
  -i "$cert_dir/root-ca.crt" >/dev/null 2>&1

export TASK_MANAGER_CERT_DIR="$cert_dir"
export NODE_EXTRA_CA_CERTS="$cert_dir/root-ca.crt"
