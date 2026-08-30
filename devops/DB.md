# Database — production & local

## Source of truth (production)

| Item | Value |
|------|--------|
| Host | Docker container `vapp_sqlserver_prod` on `195.24.237.132` |
| Database name | **`DbVapp`** only |
| API config | `DatabaseProvider=Docker` → `ConnectionStrings:DockerConnection` |
| Domains | `https://vapplication.ir` · `https://api.v-application.ir` |

Do **not** use any other SQL host or database name for prod data (no `aDb_Vapp`, no `193.141.*`, no top-level `defultConnection` / `localConnection`).

## Local development

| Provider | When | Connection key |
|----------|------|----------------|
| `Local` | SQL on Windows host | `ConnectionStrings:LocalConnection` → often `DbVappLocal` |
| `LocalDocker` | SQL in Docker, API on Mac/host | `ConnectionStrings:LocalDockerHostConnection` → `DbVapp` on `localhost,1436` |
| `Docker` | API + SQL both in compose | `ConnectionStrings:DockerConnection` → `DbVapp` |

## Inspect / mutate prod data

```bash
ssh vapp-prod
SA_PASSWORD=$(docker inspect vapp_sqlserver_prod --format '{{range .Config.Env}}{{println .}}{{end}}' | awk -F= '/^SA_PASSWORD=/{print substr($0,13); exit}')
docker exec -it vapp_sqlserver_prod /opt/mssql-tools/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASSWORD" -C -d DbVapp
```

Or use Admin APIs / crawl scripts against `https://vapplication.ir`.
