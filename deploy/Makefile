# kd-802x-portal Docker ラッパー Makefile
# 使い方: make <target>
# Windows では WSL / Git Bash で実行、または `choco install make` で GnuMake を導入

SHELL := /bin/sh
.DEFAULT_GOAL := help

# MSYS2 / Git Bash でのパス自動変換 (POSIX -> Windows) を無効化。
# 例: `/usr/local/bin/init-vault.sh` が `C:/msys64/usr/local/bin/init-vault.sh` に化けるのを防ぐ。
# Linux / macOS では未定義のまま無視されるので互換性に影響なし。
export MSYS_NO_PATHCONV := 1
export MSYS2_ARG_CONV_EXCL := *

ENV_FILE     := .env
ENV_EXAMPLE  := .env.example
COMPOSE      := docker compose

# 色付け (TTY のときだけ)
ifeq ($(shell test -t 1 && echo 1),1)
	C_INFO  := \033[36m
	C_OK    := \033[32m
	C_WARN  := \033[33m
	C_RESET := \033[0m
else
	C_INFO  :=
	C_OK    :=
	C_WARN  :=
	C_RESET :=
endif

define step
	@printf '$(C_INFO)==> %s$(C_RESET)\n' "$(1)"
endef

define ok
	@printf '$(C_OK)==> %s$(C_RESET)\n' "$(1)"
endef

define warn
	@printf '$(C_WARN)==> %s$(C_RESET)\n' "$(1)"
endef

.PHONY: help check-env init up dev down restart vault-init vault-unseal logs status ps rebuild clean

help: ## このヘルプを表示
	@echo "kd-802x-portal Docker ラッパー"
	@echo ""
	@echo "使い方:"
	@echo "    make <target>"
	@echo ""
	@echo "ターゲット:"
	@awk 'BEGIN {FS = ":[^#]*## "} /^[a-zA-Z_%-]+:[^#]*## / {printf "    %-18s %s\n", $$1, $$2}' $(MAKEFILE_LIST)
	@echo ""
	@echo "パラメータ付きターゲット (% にサービス名を入れる):"
	@echo "    make restart-<svc>    指定サービスを再起動 (例: make restart-portal)"
	@echo "    make logs-<svc>       指定サービスのログを tail (例: make logs-portal)"
	@echo "    make shell-<svc>      指定サービスのコンテナに sh で入る (例: make shell-vault)"

check-env: ## .env の存在を確認 (なければ .env.example からコピーして停止)
	@if [ ! -f $(ENV_FILE) ]; then \
		printf '$(C_WARN)==> .env が存在しません。.env.example からコピーします$(C_RESET)\n'; \
		cp $(ENV_EXAMPLE) $(ENV_FILE); \
		printf '$(C_WARN)==> .env を編集して必要な値を埋めてから再実行してください:$(C_RESET)\n'; \
		echo '    - GOOGLE_CLIENT_ID / GOOGLE_CLIENT_SECRET'; \
		echo '    - DB_PASSWORD / DB_ROOT_PASSWORD'; \
		echo '    - RADIUS_SHARED_SECRET'; \
		echo '    - (init 後に発行) VAULT_ROLE_ID / VAULT_SECRET_ID'; \
		exit 1; \
	fi

init: check-env ## 初期セットアップ (mariadb/vault 起動 -> Vault 初期化)
	$(call step,mariadb と vault を起動)
	$(COMPOSE) up -d mariadb vault
	$(call step,Vault の起動完了を待機)
	@for i in $$(seq 1 30); do \
		if $(COMPOSE) exec -T vault vault status -format=json >/dev/null 2>&1; then \
			break; \
		fi; \
		sleep 1; \
	done
	@$(MAKE) --no-print-directory vault-init
	$(call ok,.env に VAULT_ROLE_ID / VAULT_SECRET_ID を貼り付けてから make up を実行してください)

up: check-env ## 全サービスを起動
	$(call step,すべてのサービスを起動)
	$(COMPOSE) up -d
	$(COMPOSE) ps

dev: check-env ## ローカル開発 (mariadb + vault のみ起動)
	$(call step,mariadb と vault のみ起動 (ローカル開発用))
	$(COMPOSE) up -d mariadb vault
	$(COMPOSE) ps
	@echo ""
	$(call ok,ポータルはローカル実行できます: dotnet run --project ../kd-802x-portal.csproj)

down: ## 全サービスを停止
	$(call step,サービスを停止)
	$(COMPOSE) down

restart: ## 全サービスを再起動
	$(call step,全サービスを再起動)
	$(COMPOSE) restart

restart-%: ## 指定サービスを再起動 (% にサービス名)
	$(call step,$* を再起動)
	$(COMPOSE) restart $*

vault-init: ## Vault を初期化 (Transit + AppRole)
	$(call step,Vault を初期化)
	$(COMPOSE) exec vault sh /usr/local/bin/init-vault.sh

vault-unseal: ## Vault を unseal (再起動後の必須手順)
	$(call step,Vault を unseal)
	$(COMPOSE) exec vault sh /usr/local/bin/unseal.sh

logs: ## 全サービスのログを tail
	$(COMPOSE) logs -f

logs-%: ## 指定サービスのログを tail (% にサービス名)
	$(COMPOSE) logs -f $*

status: ## 全サービスの状態を表示
	$(COMPOSE) ps

ps: status ## status のエイリアス

rebuild: ## portal イメージを再ビルドして起動
	$(call step,portal イメージを再ビルド)
	$(COMPOSE) build portal
	$(call step,portal を再起動)
	$(COMPOSE) up -d portal
	$(COMPOSE) ps portal

shell-%: ## 指定サービスのコンテナに sh で入る (% にサービス名)
	$(COMPOSE) exec $* sh

clean: ## 全停止 + ボリューム削除 (データ消失、要確認)
	$(call warn,全データを削除します (mariadb_data / vault_data / vault_init))
	@printf '本当に続行しますか? [y/N] '; \
	read confirm; \
	if [ "$$confirm" = "y" ] || [ "$$confirm" = "Y" ]; then \
		$(COMPOSE) down -v; \
		printf '$(C_OK)==> 削除完了$(C_RESET)\n'; \
	else \
		printf '$(C_WARN)==> キャンセル$(C_RESET)\n'; \
	fi
