#!/usr/bin/env bash
# nginx reverse proxy (+ front: docker proxy یا static root)
# reuse: SERVER_IP، FRONT_STATIC_ROOT (اگر set → static؛ وگرنه proxy :3005)
#
# Usage:
#   bash apply-nginx.sh
#   FRONT_STATIC_ROOT=/var/www/vapp-admin bash apply-nginx.sh
set -euo pipefail

SERVER_IP="${SERVER_IP:-185.116.162.233}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-}"
DEST="/etc/nginx/sites-available/vapp"

# host deploy: static در /var/www/vapp-admin — اگر unset بود و فایل هست، auto-detect
if [[ -z "$FRONT_STATIC_ROOT" && "${FRONT_DEPLOY_MODE:-host}" == "host" && -f /var/www/vapp-admin/index.html ]]; then
  FRONT_STATIC_ROOT=/var/www/vapp-admin
fi

if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  sudo SERVER_IP="$SERVER_IP" FRONT_STATIC_ROOT="$FRONT_STATIC_ROOT" bash "$0"
  exit $?
fi

if [[ -n "$FRONT_STATIC_ROOT" ]]; then
  FRONT_BLOCK="    location / {
        root ${FRONT_STATIC_ROOT};
        index index.html;
        try_files \$uri \$uri/ /index.html;
    }"
else
  FRONT_BLOCK='    location / {
        proxy_pass http://127.0.0.1:3005;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $scheme;
    }'
fi

cat >"$DEST" <<NGINX
server {
    listen 80;
    listen [::]:80;
    server_name ${SERVER_IP};

    client_max_body_size 2048M;

    location /swagger {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }

    location /api {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Proto \$scheme;
        proxy_read_timeout 600s;
        proxy_send_timeout 600s;
    }

    location /health {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
    }

    location /hangfire {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
    }

    location /uploads {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        client_max_body_size 2048M;
        proxy_read_timeout 600s;
    }

${FRONT_BLOCK}
}
NGINX

ln -sf "$DEST" /etc/nginx/sites-enabled/vapp
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx
if [[ -n "$FRONT_STATIC_ROOT" ]]; then
  echo "OK: nginx static front → $FRONT_STATIC_ROOT"
else
  echo "OK: nginx docker front → 127.0.0.1:3005"
fi
