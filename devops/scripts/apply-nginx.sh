#!/usr/bin/env bash
# nginx reverse proxy — API + Admin + Public (فرم/گردونه SMS)
# اپ روی DOMAIN_HOST (vapplication.ir) — درگاه جدا روی GATEWAY_HOST (api.v-application.ir)
#
# Usage:
#   bash apply-nginx.sh
#   DOMAIN_HOST=vapplication.ir bash apply-nginx.sh
#   FRONT_STATIC_ROOT=/var/www/vapp-admin PUBLIC_STATIC_ROOT=/var/www/vapp-public bash apply-nginx.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
# shellcheck source=lib/load-server-conf.sh
source "$SCRIPT_DIR/lib/load-server-conf.sh"
# shellcheck source=lib/resolve-nginx-domain.sh
source "$SCRIPT_DIR/lib/resolve-nginx-domain.sh"

SERVER_IP="${SERVER_IP:-195.24.237.132}"
GATEWAY_HOST="${GATEWAY_HOST:-api.v-application.ir}"
FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-}"
PUBLIC_STATIC_ROOT="${PUBLIC_STATIC_ROOT:-}"
PUBLIC_PORT="${PUBLIC_PORT:-3006}"
DEST="/etc/nginx/sites-available/vapp"
DEST_GW="/etc/nginx/sites-available/vapp-gateway"

if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  sudo_args=(
    SERVER_IP="$SERVER_IP"
    DOMAIN="${DOMAIN:-}"
    GATEWAY_HOST="$GATEWAY_HOST"
    FRONT_STATIC_ROOT="$FRONT_STATIC_ROOT"
    PUBLIC_STATIC_ROOT="$PUBLIC_STATIC_ROOT"
    PUBLIC_PORT="$PUBLIC_PORT"
  )
  if [[ -n "${DOMAIN_HOST+x}" ]]; then
    sudo_args+=(DOMAIN_HOST="$DOMAIN_HOST")
  fi
  sudo "${sudo_args[@]}" bash "$0"
  exit $?
fi

resolve_nginx_domain_host

# Always accept localhost health-checks + public IP (and domain when set).
if [[ -n "$DOMAIN_HOST" ]]; then
  if [[ "${DOMAIN_SKIP_WWW:-0}" == "1" ]] || [[ "$DOMAIN_HOST" == *.*.* ]]; then
    SERVER_NAMES="${DOMAIN_HOST} ${SERVER_IP} 127.0.0.1 localhost"
  else
    SERVER_NAMES="${DOMAIN_HOST} www.${DOMAIN_HOST} ${SERVER_IP} 127.0.0.1 localhost"
  fi
else
  SERVER_NAMES="${SERVER_IP} 127.0.0.1 localhost"
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
if [[ -n "$PUBLIC_STATIC_ROOT" && ! -f "${PUBLIC_STATIC_ROOT}/index.html" ]]; then
  echo "WARN: ${PUBLIC_STATIC_ROOT}/index.html missing — public will proxy :${PUBLIC_PORT} (often 502)" >&2
  PUBLIC_STATIC_ROOT=""
fi

if [[ -n "$PUBLIC_STATIC_ROOT" ]]; then
  PUBLIC_BLOCK="    # Public_Vapp — لینک SMS فرم و گردونه
    location /public-assets/ {
        root ${PUBLIC_STATIC_ROOT};
        expires 7d;
        add_header Cache-Control \"public, immutable\";
        access_log off;
    }

    location ~* ^/(vapp-logo\\.png|form-bg\\.jpg|gift-icon(-gold)?\\.svg|user(-circle)?-icon\\.svg|phone-icon\\.svg|arrow-icon\\.svg|wheel\\.svg)\$ {
        root ${PUBLIC_STATIC_ROOT};
        expires 7d;
        access_log off;
    }

    location ~* ^/fonts/Shabnam[^/]*\\.(woff2?|ttf)\$ {
        root ${PUBLIC_STATIC_ROOT};
        expires 30d;
        access_log off;
    }

    location ~ ^/(salon-profile|booking-reservation|lottery-result|service-info|service-pricing|reservation-success)/ {
        root ${PUBLIC_STATIC_ROOT};
        expires 7d;
        access_log off;
    }

    location ~ ^/(form|wheel|card|book|preview)(/.*)?$ {
        root ${PUBLIC_STATIC_ROOT};
        try_files /index.html =404;
    }"
else
  PUBLIC_BLOCK="    location /public-assets/ {
        proxy_pass http://127.0.0.1:${PUBLIC_PORT};
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
        proxy_connect_timeout 2s;
        proxy_read_timeout 10s;
    }

    location ~* ^/(vapp-logo\\.png|form-bg\\.jpg|gift-icon(-gold)?\\.svg|user(-circle)?-icon\\.svg|phone-icon\\.svg|arrow-icon\\.svg|wheel\\.svg)\$ {
        proxy_pass http://127.0.0.1:${PUBLIC_PORT};
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
        proxy_connect_timeout 2s;
    }

    location ~ ^/(salon-profile|booking-reservation|lottery-result|service-info|service-pricing|reservation-success)/ {
        proxy_pass http://127.0.0.1:${PUBLIC_PORT};
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
        proxy_connect_timeout 2s;
    }

    location ~ ^/(form|wheel|card|book|preview)(/|\$) {
        proxy_pass http://127.0.0.1:${PUBLIC_PORT};
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Host \$host;
        proxy_set_header X-Forwarded-Proto \$forwarded_proto;
        proxy_connect_timeout 2s;
        proxy_read_timeout 10s;
    }"
fi

if [[ -n "$FRONT_STATIC_ROOT" ]]; then
  FRONT_BLOCK="    location / {
        root ${FRONT_STATIC_ROOT};
        index index.html;
        try_files \$uri \$uri/ @admin_vapp;
    }

    location @admin_vapp {
        root ${FRONT_STATIC_ROOT};
        rewrite ^ /index.html break;
    }"
else
  FRONT_BLOCK='    location / {
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

API_LOCATIONS=$(cat <<'LOC'
    location /swagger {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto $forwarded_proto;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_redirect off;
    }

    location /api {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto $forwarded_proto;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_read_timeout 600s;
        proxy_send_timeout 600s;
    }

    location /health {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Proto $forwarded_proto;
    }

    location /hangfire {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto $forwarded_proto;
    }

    location /uploads {
        proxy_pass http://127.0.0.1:8080;
        proxy_http_version 1.1;
        proxy_set_header Host $host;
        proxy_set_header X-Forwarded-Host $host;
        proxy_set_header X-Forwarded-Proto $forwarded_proto;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        client_max_body_size 2048M;
        proxy_read_timeout 600s;
    }
LOC
)

# Backup current combined config once (before we overwrite app site)
if [[ -f "$DEST" && ! -f /etc/nginx/sites-available/vapp.bak.pre-split ]]; then
  cp -a "$DEST" /etc/nginx/sites-available/vapp.bak.pre-split
fi

APP_CERT_DIR=""
if [[ -n "$DOMAIN_HOST" && -f "/etc/letsencrypt/live/${DOMAIN_HOST}/fullchain.pem" ]]; then
  APP_CERT_DIR="/etc/letsencrypt/live/${DOMAIN_HOST}"
fi

# Never silently downgrade HTTPS → HTTP when domain is configured but cert is missing.
if [[ -n "$DOMAIN_HOST" && -z "$APP_CERT_DIR" && -f "$DEST" ]] \
  && grep -q 'listen 443 ssl' "$DEST" 2>/dev/null; then
  echo "ERROR: DOMAIN_HOST=$DOMAIN_HOST but Let's Encrypt cert missing — refusing HTTPS downgrade." >&2
  echo "       Fix: bash $SCRIPT_DIR/switch-to-domain.sh --certbot" >&2
  echo "       IP-only: DOMAIN_HOST= bash $SCRIPT_DIR/switch-to-domain.sh --ip-only" >&2
  exit 1
fi

# App vhost — preserve HTTPS when Let's Encrypt cert exists for DOMAIN_HOST
if [[ -n "$APP_CERT_DIR" ]]; then
  cat >"$DEST" <<NGINX
map \$http_x_forwarded_proto \$forwarded_proto {
    default \$http_x_forwarded_proto;
    ''      \$scheme;
}

${CF_REAL_IP}server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name ${SERVER_NAMES};

    ssl_certificate ${APP_CERT_DIR}/fullchain.pem;
    ssl_certificate_key ${APP_CERT_DIR}/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    client_max_body_size 2048M;

${API_LOCATIONS}

${PUBLIC_BLOCK}

${FRONT_BLOCK}
}

server {
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name ${SERVER_NAMES};
    return 301 https://\$host\$request_uri;
}
NGINX
  echo "OK: app nginx with existing SSL cert → ${DOMAIN_HOST}"
else
  cat >"$DEST" <<NGINX
map \$http_x_forwarded_proto \$forwarded_proto {
    default \$http_x_forwarded_proto;
    ''      \$scheme;
}

${CF_REAL_IP}server {
    listen 80 default_server;
    listen [::]:80 default_server;
    server_name ${SERVER_NAMES};

    client_max_body_size 2048M;

${API_LOCATIONS}

${PUBLIC_BLOCK}

${FRONT_BLOCK}
}
NGINX
fi

# Gateway site: keep existing file; otherwise extract from backup or write from cert
if [[ ! -f "$DEST_GW" ]]; then
  BAK=/etc/nginx/sites-available/vapp.bak.pre-split
  if [[ -f "$BAK" ]] && grep -q "ssl_certificate /etc/letsencrypt/live/${GATEWAY_HOST}/" "$BAK"; then
    python3 - "$GATEWAY_HOST" "$BAK" "$DEST_GW" <<'PY'
import re, sys
host, src, dst = sys.argv[1], sys.argv[2], sys.argv[3]
text = open(src, encoding="utf-8").read()
blocks = []
for m in re.finditer(r"server\s*\{", text):
    start = m.start()
    i = m.end() - 1
    depth = 0
    for j in range(i, len(text)):
        if text[j] == "{":
            depth += 1
        elif text[j] == "}":
            depth -= 1
            if depth == 0:
                block = text[start : j + 1]
                if host in block:
                    blocks.append(block)
                break
open(dst, "w", encoding="utf-8").write("\n\n".join(blocks) + "\n")
print(f"extracted {len(blocks)} gateway blocks for {host}")
PY
  else
    CERT_DIR="/etc/letsencrypt/live/${GATEWAY_HOST}"
    if [[ -f "${CERT_DIR}/fullchain.pem" && -f "${CERT_DIR}/privkey.pem" ]]; then
      cat >"$DEST_GW" <<GW
# Payment gateway — ${GATEWAY_HOST}
server {
    listen 443 ssl;
    listen [::]:443 ssl;
    server_name ${GATEWAY_HOST};

    ssl_certificate ${CERT_DIR}/fullchain.pem;
    ssl_certificate_key ${CERT_DIR}/privkey.pem;
    include /etc/letsencrypt/options-ssl-nginx.conf;
    ssl_dhparam /etc/letsencrypt/ssl-dhparams.pem;

    client_max_body_size 2048M;

${API_LOCATIONS}
}

server {
    listen 80;
    listen [::]:80;
    server_name ${GATEWAY_HOST};
    return 301 https://\$host\$request_uri;
}
GW
    else
      echo "ERROR: no gateway cert and no backup to extract for ${GATEWAY_HOST}" >&2
      exit 1
    fi
  fi
  echo "OK: created gateway nginx → $DEST_GW"
else
  echo "OK: keep existing gateway nginx → $DEST_GW"
fi

ln -sf "$DEST" /etc/nginx/sites-enabled/vapp
ln -sf "$DEST_GW" /etc/nginx/sites-enabled/vapp-gateway
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl reload nginx

echo "OK: nginx app server_name → ${SERVER_NAMES}"
echo "OK: nginx gateway → ${GATEWAY_HOST}"
if [[ -n "$FRONT_STATIC_ROOT" ]]; then
  echo "OK: nginx admin static → $FRONT_STATIC_ROOT"
else
  echo "OK: nginx admin docker → 127.0.0.1:3005"
fi
if [[ -n "$PUBLIC_STATIC_ROOT" ]]; then
  echo "OK: nginx public static → $PUBLIC_STATIC_ROOT (/form, /wheel, /card, /book, /preview)"
  # shellcheck source=lib/nginx-http.sh
  source "$SCRIPT_DIR/lib/nginx-http.sh"
  if ! verify_public_routes "$SERVER_IP"; then
    echo "ERROR: Public static configured but routes not 200 — fix nginx or rebuild Public" >&2
    exit 1
  fi
  echo "OK: Public routes verified 200"
else
  echo "OK: nginx public docker → 127.0.0.1:${PUBLIC_PORT} (/form, /wheel)"
  echo "WARN: no /var/www/vapp-public/index.html — prefer: bash $SCRIPT_DIR/deploy-public-front-host.sh" >&2
fi

# Fail fast if app HTTPS cert does not match DOMAIN_HOST (prevents gateway cert bleed-through).
if [[ -n "$APP_CERT_DIR" && -n "$DOMAIN_HOST" ]]; then
  # shellcheck source=lib/nginx-http.sh
  source "$SCRIPT_DIR/lib/nginx-http.sh"
  if ! verify_https_cert_retry "$DOMAIN_HOST"; then
    echo "ERROR: HTTPS cert mismatch for https://${DOMAIN_HOST}/ — run: bash $SCRIPT_DIR/switch-to-domain.sh" >&2
    exit 1
  fi
  echo "OK: HTTPS cert verified → https://${DOMAIN_HOST}/"
  if [[ -f "/etc/letsencrypt/live/${GATEWAY_HOST}/fullchain.pem" ]]; then
    if ! verify_https_cert_retry "$GATEWAY_HOST"; then
      echo "ERROR: HTTPS cert mismatch for https://${GATEWAY_HOST}/" >&2
      exit 1
    fi
    echo "OK: HTTPS cert verified → https://${GATEWAY_HOST}/"
  fi
fi
