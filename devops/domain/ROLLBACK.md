# اضطراری — سرو موقت روی IP (همان سرور)

> Production عادی: **`https://vapplication.ir`** + **`https://api.v-application.ir`**.  
> این فایل فقط برای قطعی دامنه/SSL است — **همان** سرور `195.24.237.132` و همان DB **`DbVapp`**.  
> به SQL قدیمی / دیتابیس دیگر وصل نشوید.

> **توجه:** فایل nginx درگاه (`/etc/nginx/sites-available/vapp-gateway` برای `api.v-application.ir`) را پاک نکنید.

```bash
ssh vapp-prod
cd ~/Api_Vapp_Manually
bash devops/scripts/switch-to-domain.sh --ip-only
bash devops/scripts/health-check.sh
```

یا دستی:

1. در `docker/.env`: `PUBLIC_FRONTEND_URL` و `FORM_*` / `WHEEL_*` / `CARD_*` / `BOOKING_*` را موقتاً به `http://195.24.237.132/...` ببرید
2. `PUBLIC_API_BASE_URL` و `ZarinPal__CallbackUrl` را روی `https://api.v-application.ir` نگه دارید (درگاه)
3. `DOMAIN_HOST=` خالی — `GATEWAY_HOST=api.v-application.ir bash devops/scripts/apply-nginx.sh`
4. `docker compose ... up -d --no-deps --force-recreate api`

بعد از رفع دامنه دوباره `switch-to-domain.sh` (حالت دامنه) را بزنید.
