# برگشت به حالت IP — `http://185.116.162.233`

```bash
ssh vapp-prod
cd ~/Api_Vapp_Manually
bash devops/scripts/switch-to-domain.sh --ip-only
bash devops/scripts/health-check.sh
```

یا دستی:

1. در `docker/.env`: `PUBLIC_*` و `FORM_*` / `WHEEL_*` را به `http://185.116.162.233` برگردانید
2. `DOMAIN_HOST=` خالی — `bash devops/scripts/apply-nginx.sh`
3. `docker compose ... up -d --no-deps --force-recreate api`
