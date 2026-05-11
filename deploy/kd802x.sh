#!/usr/bin/env bash
# kd-802x-portal Docker ラッパースクリプト (Linux / Proxmox ホスト)

set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
cd "${SCRIPT_DIR}"

step() { printf '\033[36m==> %s\033[0m\n' "$1"; }
done_msg() { printf '\033[32m==> %s\033[0m\n' "$1"; }
warn() { printf '\033[33m==> %s\033[0m\n' "$1"; }

show_help() {
    cat <<'EOF'
kd-802x-portal Docker ラッパースクリプト

使い方:
    ./kd802x.sh <command> [args...]

コマンド:
    init           初期セットアップ (.env 作成 -> mariadb/vault 起動 -> Vault 初期化)
    up             すべてのサービスを起動 (portal + mariadb + vault + freeradius)
    dev            ローカル開発用 (mariadb + vault のみ起動)
    down           すべてのサービスを停止
    restart [svc]  指定サービスを再起動 (省略時は全部)
    vault-init     Vault を初期化 (Transit + AppRole + Policy)
    vault-unseal   Vault を unseal (コンテナ再起動後の必須手順)
    logs [svc]     ログを tail (省略時は全部)
    status         全サービスの状態を表示
    rebuild        portal イメージを再ビルドして起動
    shell <svc>    サービスのコンテナに sh で入る
    clean          全停止 + ボリューム削除 (データ消失、要確認)
    help           このヘルプを表示

例:
    ./kd802x.sh init
    ./kd802x.sh dev
    ./kd802x.sh logs portal
    ./kd802x.sh shell vault
EOF
}

ensure_env() {
    if [ ! -f .env ]; then
        warn '.env が存在しません。.env.example からコピーします'
        cp .env.example .env
        warn '.env を編集して必要な値を埋めてから再実行してください:'
        warn '  - GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET'
        warn '  - DB_PASSWORD / DB_ROOT_PASSWORD'
        warn '  - RADIUS_SHARED_SECRET'
        warn '  - (init 後に発行する) VAULT_ROLE_ID / VAULT_SECRET_ID'
        exit 1
    fi
}

cmd_vault_init() {
    step 'Vault を初期化 (init-vault.sh)'
    docker compose exec vault sh /usr/local/bin/init-vault.sh
}

cmd_vault_unseal() {
    step 'Vault を unseal'
    docker compose exec vault sh /usr/local/bin/unseal.sh
}

cmd_init() {
    ensure_env
    step 'mariadb と vault を起動'
    docker compose up -d mariadb vault
    step 'Vault の起動完了を待機'
    for i in $(seq 1 30); do
        if docker compose exec -T vault vault status -format=json >/dev/null 2>&1; then
            break
        fi
        sleep 1
    done
    cmd_vault_init
    done_msg '.env に VAULT_ROLE_ID / VAULT_SECRET_ID を貼り付けてから ./kd802x.sh up を実行してください'
}

cmd_up() {
    ensure_env
    step 'すべてのサービスを起動'
    docker compose up -d
    docker compose ps
}

cmd_dev() {
    ensure_env
    step 'mariadb と vault のみ起動 (ローカル開発用)'
    docker compose up -d mariadb vault
    docker compose ps
    echo
    done_msg 'ポータルはローカル実行できます:'
    done_msg '    dotnet run --project ../kd-802x-portal.csproj'
}

cmd_down() {
    step 'サービスを停止'
    docker compose down
}

cmd_restart() {
    if [ $# -eq 0 ]; then
        step '全サービスを再起動'
        docker compose restart
    else
        step "$* を再起動"
        docker compose restart "$@"
    fi
}

cmd_logs() {
    if [ $# -eq 0 ]; then
        docker compose logs -f
    else
        docker compose logs -f "$@"
    fi
}

cmd_status() {
    docker compose ps
}

cmd_rebuild() {
    step 'portal イメージを再ビルド'
    docker compose build portal
    step 'portal を再起動'
    docker compose up -d portal
    docker compose ps portal
}

cmd_shell() {
    if [ $# -eq 0 ]; then
        warn 'サービス名を指定してください (例: ./kd802x.sh shell vault)'
        exit 1
    fi
    docker compose exec "$1" sh
}

cmd_clean() {
    warn '全データを削除します (mariadb_data / vault_data / vault_init のボリュームを含む)'
    read -r -p '本当に続行しますか? [y/N] ' confirm
    if [ "${confirm}" != "y" ] && [ "${confirm}" != "Y" ]; then
        warn 'キャンセル'
        return
    fi
    docker compose down -v
    done_msg '削除完了'
}

case "${1:-help}" in
    init)         shift; cmd_init "$@" ;;
    up)           shift; cmd_up "$@" ;;
    dev)          shift; cmd_dev "$@" ;;
    down)         shift; cmd_down "$@" ;;
    restart)      shift; cmd_restart "$@" ;;
    vault-init)   shift; cmd_vault_init "$@" ;;
    vault-unseal) shift; cmd_vault_unseal "$@" ;;
    logs)         shift; cmd_logs "$@" ;;
    status)       shift; cmd_status "$@" ;;
    rebuild)      shift; cmd_rebuild "$@" ;;
    shell)        shift; cmd_shell "$@" ;;
    clean)        shift; cmd_clean "$@" ;;
    help|--help|-h) show_help ;;
    *)            show_help ;;
esac
