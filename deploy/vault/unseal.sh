#!/bin/sh
# コンテナ再起動時の自動 unseal。init.json が存在しない場合 (初回) は何もしない。

set -eu

VAULT_ADDR="${VAULT_ADDR:-http://127.0.0.1:8200}"
INIT_FILE="/vault/init/init.json"

# Vault がリッスンするまで待機
i=0
until vault status >/dev/null 2>&1 || [ "$i" -ge 30 ]; do
    sleep 1
    i=$((i + 1))
done

if [ ! -f "${INIT_FILE}" ]; then
    echo "init.json が見つかりません。init-vault.sh を実行して初期化してください。"
    exit 0
fi

if vault status -format=json | grep -q '"sealed": true'; then
    UNSEAL_KEY=$(grep '"unseal_keys_b64"' -A1 "${INIT_FILE}" | tail -1 | sed -E 's/.*"([^"]+)".*/\1/')
    vault operator unseal "${UNSEAL_KEY}" >/dev/null
    echo "Vault を unseal しました。"
fi
