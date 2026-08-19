# برگشت به حالت IP — `http://195.24.237.132`

```bash
ssh vapp-prod
cd ~/Api_Vapp_Manually
bash devops/scripts/switch-to-domain.sh --ip-only
bash devops/scripts/health-check.sh
```

یا دستی:

1. در `docker/.env`: `PUBLIC_*` و `FORM_*` / `WHEEL_*` را به `http://195.24.237.132` برگردانید
2. `DOMAIN_HOST=` خالی — `bash devops/scripts/apply-nginx.sh`
3. `docker compose ... up -d --no-deps --force-recreate api`
