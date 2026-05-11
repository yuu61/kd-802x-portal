storage "file" {
  path = "/vault/data"
}

listener "tcp" {
  address     = "0.0.0.0:8200"
  tls_disable = 1
}

api_addr = "http://vault:8200"
# cap_add: IPC_LOCK で mlock を許可しているので disable_mlock は false (既定) のまま
ui = true
