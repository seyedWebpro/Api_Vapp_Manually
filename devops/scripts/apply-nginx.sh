#!/usr/bin/env bash
# nginx reverse proxy — API + Admin + Public (فرم/گردونه SMS)
# reuse: FRONT_STATIC_ROOT (Admin)، PUBLIC_STATIC_ROOT (Public_Vapp)، DOMAIN_HOST
#
# Usage:
#   bash apply-nginx.sh
#   DOMAIN_HOST=ok-sms.ir bash apply-nginx.sh
#   FRONT_STATIC_ROOT=/var/www/vapp-admin PUBLIC_STATIC_ROOT=/var/www/vapp-public bash apply-nginx.sh
set -euo pipefail

SERVER_IP="${SERVER_IP:-185.116.162.233}"
DOMAIN_HOST="${DOMAIN_HOST:-}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-}"
PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-}"
PUBLIC_PORT="${PUBLIC_PORT:-3006}"
DEST="/etc/nginx/sites-available/vapp"

if [[ -n "$DOMAIN_HOST" ]]; then
  SERVER_NAMES="${DOMAIN_HOST} www.${DOMAIN_HOST} ${SERVER_IP}"
else
  SERVER_NAMES="${SERVER_IP}"
fi

if [[ -z "$FRONT_STATIC_ROOT" && "${FRONT_DEPLOY_MODE:-host}" == "host" && -f /var/www/vapp-admin/index.html ]]; then
  FRONT_STATIC_ROOT=/var/www/vapp-admin
fi

if [[ -n "$FRONT_STATIC_ROOT" && ! -f "${FRONT_STATIC_ROOT}/index.html" ]]; then
  echo "WARN: ${FRONT_STATIC_ROOT}/index.html not found — admin will use docker :3005" >&2
  FRONT_STATIC_ROOT=""
fi

if [[ -z "$PUBLIC_STATIC_ROOT" && -f /var/www/vapp-public/index.html ]]; then
  PUBLIC_STATIC_ROOT=/var/www/vapp-public
fi

if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  sudo SERVER_IP="$SERVER_IP" \
    DOMAIN_HOST="$DOMAIN_HOST" \
    FRONT_STATIC_ROOT="$FRONT_STATIC_ROOT" \
    PUBLIC_STATIC_ROOT="$PUBLIC_STATIC_ROOT" \
    PUBLIC_PORT="$PUBLIC_PORT" \
    bash "$0"
  exit $?
fi

if [[ -n "$PUBLIC_STATIC_ROOT" ]]; then
  PUBLIC_BLOCK="    # Public_Vapp — لینک SMS فرم و گردونه
    location /public-assets/ {
        root ${PUBLIC_STATIC_ROOT};
        expires 7d;
        add_header Cache-Control \"public, immutable\";
        access_log off;
    }

    location ~ ^/(form|wheel)(/.*)?$ {
        root ${PUBLIC_STATIC_ROOT};
        try_files \$uri \$uri/ @public_vapp;
    }

    location @public_vapp {
        root ${PUBLIC_STATIC_ROOT};
        rewrite ^ /index.html break;
    }"
else
  PUBLIC_BLOCK="    # Public_Vapp — docker :${PUBLIC_PORT}
    location /public-assets/ {
        proxy_pass http://127.0.0.1:${PUBLIC_PORT};
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
    }

    location ~ ^/(form|wheel)(/|\$) {
        proxy_pass http://127.0.0.1:${PUBLIC_PORT};
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
    }"
fi

if [[ -n "$FRONT_STATIC_ROOT" ]]; then
  FRONT_BLOCK="    # Admin_Vapp (static)
    location / {
        root ${FRONT_STATIC_ROOT};
        index index.html;
        try_files \$uri \$uri/ @admin_vapp;
    }

    location @admin_vapp {
        root ${FRONT_STATIC_ROOT};
        rewrite ^ /index.html break;
    }"
else
  FRONT_BLOCK='    # Admin_Vapp (docker)
    location / {
        proxy_pass http://127.0.0.1:3005;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $forwarded_proto;
        proxy_cache_bypass $http_upgrade;
    }'
fi

CF_REAL_IP=""
if [[ -n "$DOMAIN_HOST" ]]; then
  CF_REAL_IP='set_real_ip_from 173.245.48.0/20;
set_real_ip_from 103.21.244.0/22;
set_real_ip_from 103.22.200.0/22;
set_real_ip_from 103.31.4.0/22;
set_real_ip_from 141.101.64.0/18;
set_real_ip_from 108.162.192.0/18;
set_real_ip_from 190.93.240.0/20;
set_real_ip_from 188.114.96.0/20;
set_real_ip_from 197.234.240.0/22;
set_real_ip_from 198.41.128.0/17;
set_real_ip_from 162.158.0.0/15;
set_real_ip_from 104.16.0.0/13;
set_real_ip_from 104.24.0.0/14;
set_real_ip_from 172.64.0.0/13;
set_real_ip_from 131.0.72.0/22;
real_ip_header CF-Connecting-IP;

'
fi

cat >"$DEST" <<NGINX
map \$http_x_forwarded_proto \$forwarded_proto {
    default \$http_x_forwarded_proto;
    ''      \$scheme;
}

${CF_REAL_IP}server {
    listen 80;
    listen [::]:80;
    server_name ${SERVER_NAMES};

    client_max_body_size 2048M;

    location /swagger {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_redirect off;
    }

    location /api {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        proxy_read_timeout 600s;
        proxy_send_timeout 600s;
    }

    location /health {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
    }

    location /hangfire {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
    }

    location /uploads {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
        proxy_set_header X-Real-IP \$remote_addr;
        proxy_set_header X-Forwarded-For \$proxy_add_x_forwarded_for;
        client_max_body_size 2048M;
        proxy_read_timeout 600s;
    }

${PUBLIC_BLOCK}

${FRONT_BLOCK}
}
NGINX

ln -sf "$DEST" /etc/nginx/sites-enabled/vapp
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx

echo "OK: nginx server_name → ${SERVER_NAMES}"
if [[ -n "$FRONT_STATIC_ROOT" ]]; then
  echo "OK: nginx admin static → $FRONT_STATIC_ROOT"
else
  echo "OK: nginx admin docker → 127.0.0.1:3005"
fi
if [[ -n "$PUBLIC_STATIC_ROOT" ]]; then
  echo "OK: nginx public static → $PUBLIC_STATIC_ROOT (/form, /wheel)"
else
  echo "OK: nginx public docker → 127.0.0.1:${PUBLIC_PORT} (/form, /wheel)"
fi
