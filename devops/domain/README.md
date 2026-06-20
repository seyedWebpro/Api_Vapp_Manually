# دامنه / IP — یادداشت deploy
# reuse: DNS، SSL (certbot)، PUBLIC_* URL در .env

فعلاً سرویس روی **http://185.116.162.233** بالا می‌آید.

## وقتی دامنه گرفتید

1. DNS: A record → `185.116.162.233`
2. در `devops/deploy/nginx-vapp.conf.example` مقدار `server_name` را عوض کنید
3. `PUBLIC_API_BASE_URL` و `PUBLIC_FRONTEND_URL` را در `docker/.env` به `https://yourdomain.com` تغییر دهید
4. Certbot: `sudo certbot --nginx -d yourdomain.com`
5. `bash devops/scripts/apply-nginx.sh && bash devops/scripts/deploy-server.sh --fast --wait`

## Cloudflare (اختیاری)

مثل AmazonShop می‌توانید proxy را فعال کنید؛ برای API/WebSocket معمولاً DNS-only (`grey cloud`) برای `/api` پایدارتر است.
