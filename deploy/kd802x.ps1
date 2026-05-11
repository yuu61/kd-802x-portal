#!/usr/bin/env pwsh
# kd-802x-portal Docker ラッパースクリプト (Windows 開発機 / PowerShell 7+)

[CmdletBinding()]
param(
    [Parameter(Position = 0)]
    [ValidateSet('init', 'up', 'dev', 'down', 'restart', 'vault-init', 'vault-unseal', 'logs', 'status', 'rebuild', 'shell', 'clean', 'help')]
    [string]$Command = 'help',

    [Parameter(Position = 1, ValueFromRemainingArguments = $true)]
    [string[]]$Rest
)

$ErrorActionPreference = 'Stop'
$DeployDir = Split-Path -Parent $PSCommandPath
Set-Location $DeployDir

function Write-Step($msg) { Write-Host "==> $msg" -ForegroundColor Cyan }
function Write-Done($msg) { Write-Host "==> $msg" -ForegroundColor Green }
function Write-Warn($msg) { Write-Host "==> $msg" -ForegroundColor Yellow }

function Show-Help {
    @'
kd-802x-portal Docker ラッパースクリプト

使い方:
    .\kd802x.ps1 <command> [args...]

コマンド:
    init           初期セットアップ (.env 作成 -> mariadb/vault 起動 -> Vault 初期化)
    up             すべてのサービスを起動 (portal + mariadb + vault + freeradius)
    dev            ローカル開発用 (mariadb + vault のみ起動。portal は dotnet run で別途)
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
    .\kd802x.ps1 init
    .\kd802x.ps1 dev
    .\kd802x.ps1 logs portal
    .\kd802x.ps1 shell vault
'@
}

function Test-Env {
    if (-not (Test-Path .env)) {
        Write-Warn '.env が存在しません。.env.example からコピーします'
        Copy-Item .env.example .env
        Write-Warn '.env を編集して必要な値を埋めてから再実行してください:'
        Write-Warn '  - GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET'
        Write-Warn '  - DB_PASSWORD / DB_ROOT_PASSWORD'
        Write-Warn '  - RADIUS_SHARED_SECRET'
        Write-Warn '  - (init 後に発行する) VAULT_ROLE_ID / VAULT_SECRET_ID'
        exit 1
    }
}

function Invoke-VaultInit {
    Write-Step 'Vault を初期化 (init-vault.sh)'
    docker compose exec vault sh /usr/local/bin/init-vault.sh
}

function Invoke-VaultUnseal {
    Write-Step 'Vault を unseal'
    docker compose exec vault sh /usr/local/bin/unseal.sh
}

function Invoke-Init {
    Test-Env
    Write-Step 'mariadb と vault を起動'
    docker compose up -d mariadb vault
    Write-Step 'Vault の起動完了を待機'
    $ready = $false
    for ($i = 0; $i -lt 30; $i++) {
        Start-Sleep -Seconds 1
        try {
            docker compose exec -T vault vault status -format=json *>$null
            $ready = $true
            break
        } catch { }
    }
    if (-not $ready) {
        Write-Warn 'Vault が起動完了しませんでした。docker compose logs vault を確認してください'
    }
    Invoke-VaultInit
    Write-Done '.env に VAULT_ROLE_ID / VAULT_SECRET_ID を貼り付けてから `.\kd802x.ps1 up` を実行してください'
}

function Invoke-Up {
    Test-Env
    Write-Step 'すべてのサービスを起動'
    docker compose up -d
    docker compose ps
}

function Invoke-Dev {
    Test-Env
    Write-Step 'mariadb と vault のみ起動 (ローカル開発用)'
    docker compose up -d mariadb vault
    docker compose ps
    Write-Host ''
    Write-Done 'ポータルはローカル実行できます:'
    Write-Done '    dotnet run --project ..\kd-802x-portal.csproj'
}

function Invoke-Down {
    Write-Step 'サービスを停止'
    docker compose down
}

function Invoke-Restart {
    if ($Rest.Count -eq 0) {
        Write-Step '全サービスを再起動'
        docker compose restart
    } else {
        Write-Step "$($Rest -join ' ') を再起動"
        docker compose restart @Rest
    }
}

function Invoke-Logs {
    if ($Rest.Count -eq 0) {
        docker compose logs -f
    } else {
        docker compose logs -f @Rest
    }
}

function Invoke-Status {
    docker compose ps
}

function Invoke-Rebuild {
    Write-Step 'portal イメージを再ビルド'
    docker compose build portal
    Write-Step 'portal を再起動'
    docker compose up -d portal
    docker compose ps portal
}

function Invoke-Shell {
    if ($Rest.Count -eq 0) {
        Write-Warn 'サービス名を指定してください (例: .\kd802x.ps1 shell vault)'
        exit 1
    }
    docker compose exec $Rest[0] sh
}

function Invoke-Clean {
    Write-Warn '全データを削除します (mariadb_data / vault_data / vault_init のボリュームを含む)'
    $confirmation = Read-Host '本当に続行しますか? [y/N]'
    if ($confirmation -ne 'y' -and $confirmation -ne 'Y') {
        Write-Warn 'キャンセル'
        return
    }
    docker compose down -v
    Write-Done '削除完了'
}

switch ($Command) {
    'init'         { Invoke-Init }
    'up'           { Invoke-Up }
    'dev'          { Invoke-Dev }
    'down'         { Invoke-Down }
    'restart'      { Invoke-Restart }
    'vault-init'   { Invoke-VaultInit }
    'vault-unseal' { Invoke-VaultUnseal }
    'logs'         { Invoke-Logs }
    'status'       { Invoke-Status }
    'rebuild'      { Invoke-Rebuild }
    'shell'        { Invoke-Shell }
    'clean'        { Invoke-Clean }
    default        { Show-Help }
}
