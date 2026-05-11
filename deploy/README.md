# kd-802x-portal デプロイメント (PoC)

Proxmox 上の LXC/VM 内で `docker compose up` で動かす最小構成。

## 構成

| サービス | 役割 |
|---|---|
| `portal` | ASP.NET Core 10 / Blazor Server (ID/パスワード発行ポータル) |
| `mariadb` | アカウント + 暗号化パスワード保管 |
| `vault` | HashiCorp Vault Transit (パスワード暗号化) |
| `freeradius` | 802.1x EAP-TTLS/PAP (rlm_rest 経由でポータルに検証委譲) |

## 事前準備

### Google OAuth クライアントの発行

1. [Google Cloud Console](https://console.cloud.google.com/apis/credentials) で OAuth 2.0 クライアント (Web アプリケーション) を作成
2. **承認済みのリダイレクト URI** に `https://<公開 URL>/signin-oidc` (例: `http://localhost:8080/signin-oidc`) を追加
3. クライアント ID / シークレットを `.env` に貼り付ける
4. Google Workspace 管理者に依頼し、`@st.kobedenshi.co.jp` ドメインで OIDC 認証を許可

### コンテナイメージタグ確認

`docker-compose.yml` の各タグ (`mariadb:11`, `hashicorp/vault:1.18`, `freeradius/freeradius-server:3.2`) を `docker pull <image>:<tag>` で事前確認してください。存在しなければ Docker Hub で最新の安定タグに差し替えます (例: FreeRADIUS は `3.2.5` などのパッチバージョン)。

## ラッパー (Makefile)

主要操作は `Makefile` でラップ。Linux / macOS / Proxmox ではそのまま、Windows では WSL / Git Bash で実行 (または `choco install make` で GnuMake を導入)。

```bash
make help            # ヘルプ (全ターゲット一覧)
make init            # 初期セットアップ (.env 作成 → mariadb/vault 起動 → Vault 初期化)
make dev             # ローカル開発: mariadb + vault のみ起動
make up              # 全サービス起動
make down            # 全サービス停止
make vault-unseal    # Vault 再起動後の手動 unseal
make logs            # 全サービスのログ追跡
make logs-portal     # portal のログ追跡 (logs-<svc> パターン)
make shell-vault     # vault コンテナに sh で入る (shell-<svc> パターン)
make restart-portal  # portal を再起動 (restart-<svc> パターン)
make rebuild         # portal の再ビルド + 起動
make clean           # データボリューム含めて全削除 (要確認)
```

以降の手順は手動コマンドの参考。Makefile の使用を優先してください。

## 起動手順

### 1. 環境変数を準備

```bash
cp .env.example .env
# 各値を埋める (DB / Google OAuth / RADIUS 共有鍵)
```

### 2. Vault のみ先に起動して初期化

```bash
docker compose up -d vault
sleep 3
docker compose exec vault sh /usr/local/bin/init-vault.sh
# 出力された VAULT_ROLE_ID / VAULT_SECRET_ID を .env に貼り付ける
```

### 3. 残りを起動

```bash
docker compose up -d
```

### ローカル開発時の起動 (Visual Studio / VS Code から `dotnet run` する場合)

ポータル本体だけを IDE デバッグ実行し、依存サービスは Docker で立てる。

```bash
# 1. バックエンドだけ起動 (portal / freeradius は起動しない)
docker compose up -d mariadb vault

# 2. Vault 初期化 (初回のみ)
docker compose exec vault sh /usr/local/bin/init-vault.sh

# 3. .env と appsettings.Development.json の整合
#    - .env の DB_PASSWORD と appsettings.Development.json の ConnectionString が一致していること
#    - .env の VAULT_ROLE_ID / VAULT_SECRET_ID を環境変数で渡すか、appsettings.Development.json に書く
#      例) PowerShell:
#      $env:Vault__RoleId="<role_id>"; $env:Vault__SecretId="<secret_id>"

# 4. ポータルを起動
dotnet run --project ..\kd-802x-portal.csproj
```

User Secrets (`dotnet user-secrets`) で Google ClientId / ClientSecret / Vault SecretId を管理すると安全。

### コンテナ再起動時の unseal (運用)

`docker compose restart vault` 後は自動 unseal されないため、以下を実行する。

```bash
docker compose exec vault sh /usr/local/bin/unseal.sh
```

### 4. ポータルへアクセス

`http://<host>:8080/` を開くと Google SSO へリダイレクト。`@st.kobedenshi.co.jp` ドメインで認証成功すれば `/dashboard` に着地し、初回ログイン時に 802.1x 用パスワードが自動生成される。

## 802.1x 接続

- SSID: WPA3-Enterprise / EAP-TTLS / PAP
- ユーザー名: `<学籍番号>@st.kobedenshi.co.jp`
- パスワード: ポータルの 👁 ボタンで表示 (Google 再認証必須)
- サーバ証明書: PoC では自己署名 (端末にルート CA を手動インストール)

## WLC (Cisco Catalyst 9800-CL) 連携

PoC では Proxmox 上で動く Catalyst 9800-CL (仮想 WLC) を RADIUS クライアントとして登録済み。

| 項目 | 値 |
|---|---|
| WLC WMI (RADIUS source) | `10.98.38.4/32` (VLAN100) |
| RADIUS サーバ | `10.98.38.5` (VLAN100、Proxmox 上の LXC) |
| 共有鍵 | `.env` の `WLC_SHARED_SECRET` |
| Message-Authenticator | 必須 (`require_message_authenticator = yes`) |
| 認証方式 | EAP-TTLS / 内部 PAP |

Catalyst 9800-CL 側の設定例 (CLI):

```cisco
radius server kd-802x-radius
 address ipv4 10.98.38.5 auth-port 1812 acct-port 1813
 key 0 <WLC_SHARED_SECRET と同じ値>
 retransmit 3
 timeout 5
!
aaa group server radius kd-802x-group
 server name kd-802x-radius
 ip radius source-interface Vlan100
!
aaa authentication dot1x kd-802x-dot1x group kd-802x-group
aaa authorization network kd-802x-authz group kd-802x-group
!
dot1x system-auth-control
!
wlan kd-802x-ssid 1 kd-802x
 security wpa wpa3
 security wpa akm dot1x
 security dot1x authentication-list kd-802x-dot1x
 no shutdown
```

WLC 側で `Message-Authenticator` を必ず添付するよう、`radius-server attribute 6 on-for-login-auth` を含むテンプレートを参照すること。

## 運用上の注意 (PoC)

- **Vault は Production mode**。Dev mode は禁止 (再起動で暗号鍵が消滅し DB の暗号文が全件復号不能になるため)
- `vault_init` ボリュームに `init.json` が保管される。**バックアップ必須** (これを失うと全アカウントの再発行が必要)
- 自己署名証明書は検証端末に都度信頼登録が必要
- `RADIUS_SHARED_SECRET` は FreeRADIUS ↔ ポータル間の HTTP Bearer トークン。**WLC ↔ FreeRADIUS の RADIUS Shared Secret とは別物**

## トラブルシューティング

- ポータルが Vault に接続できない → `docker compose logs vault` で sealed 状態を確認、必要なら `unseal.sh` を再実行
- Google SSO が `hd` 不一致で拒否 → `.env` の `ALLOWED_HOSTED_DOMAIN` と Google Workspace のドメインが一致しているか確認
- マイグレーションが走らない → MariaDB の起動待ちで `portal` が早く起動した可能性。`docker compose restart portal`
