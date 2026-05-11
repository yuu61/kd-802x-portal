#!/bin/sh
# 初回手動実行用: Vault を初期化 -> Transit + AppRole を設定 -> 値を出力
# PoC 用に key_shares=1 key_threshold=1 で運用。本番は 5/3 等に変更。

set -eu

VAULT_ADDR="${VAULT_ADDR:-http://127.0.0.1:8200}"
INIT_FILE="/vault/init/init.json"

if [ -f "${INIT_FILE}" ]; then
    echo "Vault は既に初期化されています: ${INIT_FILE}"
else
    echo "Vault を初期化します..."
    vault operator init -key-shares=1 -key-threshold=1 -format=json > "${INIT_FILE}"
fi

UNSEAL_KEY=$(grep '"unseal_keys_b64"' -A1 "${INIT_FILE}" | tail -1 | sed -E 's/.*"([^"]+)".*/\1/')
ROOT_TOKEN=$(grep '"root_token"' "${INIT_FILE}" | sed -E 's/.*"root_token": "([^"]+)".*/\1/')

# Unseal
if vault status -format=json | grep -q '"sealed": true'; then
    vault operator unseal "${UNSEAL_KEY}"
fi

export VAULT_TOKEN="${ROOT_TOKEN}"

# Transit Secrets Engine
vault secrets enable transit 2>/dev/null || true
vault write -f transit/keys/kd-802x-portal-password >/dev/null

# Policy
vault policy write kd-802x-portal-policy - <<'POLICY'
path "transit/encrypt/kd-802x-portal-password" { capabilities = ["update"] }
path "transit/decrypt/kd-802x-portal-password" { capabilities = ["update"] }
POLICY

# AppRole
vault auth enable approle 2>/dev/null || true
vault write auth/approle/role/kd-802x-portal \
    token_policies="kd-802x-portal-policy" \
    token_ttl=1h token_max_ttl=4h >/dev/null

ROLE_ID=$(vault read -field=role_id auth/approle/role/kd-802x-portal/role-id)
SECRET_ID=$(vault write -field=secret_id -f auth/approle/role/kd-802x-portal/secret-id)

echo ""
echo "==== .env に貼り付ける値 ===="
echo "VAULT_ROLE_ID=${ROLE_ID}"
echo "VAULT_SECRET_ID=${SECRET_ID}"
echo ""
echo "==== 参考 (init.json に保管済み) ===="
echo "VAULT_ROOT_TOKEN=${ROOT_TOKEN}"
echo "VAULT_UNSEAL_KEY=${UNSEAL_KEY}"
