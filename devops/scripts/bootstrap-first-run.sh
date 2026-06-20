#!/usr/bin/env bash
# Bootstrap یک‌بار — Docker، Nginx، .env، build API+front
# reuse: SERVER_IP، مسیر repo، Dockerfile front، compose path، env vars
# Usage: sudo bash bootstrap-first-run.sh
set -euo pipefail

API_DIR="${API_DIR:-$HOME/Api_Vapp_Manually}"
FRONT_DIR="${FRONT_DIR:-$HOME/Admin_Vapp}"
SERVER_IP="${SERVER_IP:-185.116.162.233}"
SECRETS_FILE="${SECRETS_FILE:-$HOME/vapp-secrets.txt}"

log() { echo "[$(date -Is)] $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

[[ -d "$API_DIR" ]] || die "API not found: $API_DIR — clone first"
[[ -d "$FRONT_DIR" ]] || die "Admin not found: $FRONT_DIR — clone first"

log "=== Vapp bootstrap started ==="

# --- 1) Docker + Nginx + deps ---
export DEBIAN_FRONTEND=noninteractive
apt-get update -qq
apt-get install -y -qq curl git nginx ufw ca-certificates gnupg lsb-release openssl

if ! command -v docker >/dev/null 2>&1; then
  install -m 0755 -d /etc/apt/keyrings
  curl -fsSL https://download.docker.com/linux/ubuntu/gpg | gpg --dearmor -o /etc/apt/keyrings/docker.gpg
  chmod a+r /etc/apt/keyrings/docker.gpg
  echo "deb [arch=$(dpkg --print-architecture) signed-by=/etc/apt/keyrings/docker.gpg] https://download.docker.com/linux/ubuntu $(. /etc/os-release && echo "$VERSION_CODENAME") stable" \
    > /etc/apt/sources.list.d/docker.list
  apt-get update -qq
  apt-get install -y -qq docker-ce docker-ce-cli containerd.io docker-buildx-plugin docker-compose-plugin
  systemctl enable --now docker
fi

ufw allow OpenSSH 2>/dev/null || ufw allow 22/tcp
ufw allow 80/tcp
ufw allow 443/tcp
echo "y" | ufw enable 2>/dev/null || true

# --- 2) dirs ---
mkdir -p "$API_DIR/wwwroot/uploads" "$API_DIR/log" "$API_DIR/backups/daily" "$API_DIR/backups/weekly" "$API_DIR/backups/logs"

# --- 3) .env auto ---
ENV_FILE="$API_DIR/docker/.env"
if [[ ! -f "$ENV_FILE" ]]; then
  SA_PASSWORD="Vapp$(openssl rand -hex 10)!Secure"
  JWT_SECRET="$(openssl rand -base64 36 | tr -d '\n/+=' | head -c 48)"
  cat >"$ENV_FILE" <<EOF
SA_PASSWORD=${SA_PASSWORD}
API_PORT_MAPPING=127.0.0.1:8080:8080
PUBLIC_API_BASE_URL=http://${SERVER_IP}
PUBLIC_FRONTEND_URL=http://${SERVER_IP}
Jwt__Secret=${JWT_SECRET}
EOF
  chmod 600 "$ENV_FILE"
  cat >"$SECRETS_FILE" <<EOF
# Vapp production secrets — $(date -Is)
# نگه دارید — برای بازیابی رمز DB و JWT
SA_PASSWORD=${SA_PASSWORD}
Jwt__Secret=${JWT_SECRET}
EOF
  chmod 600 "$SECRETS_FILE"
  log "Created $ENV_FILE and saved secrets to $SECRETS_FILE"
else
  log "Using existing $ENV_FILE"
fi

# --- 4) Nginx reverse proxy ---
cat >/etc/nginx/sites-available/vapp <<NGINX
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

    location / {
        proxy_pass http://127.0.0.1:3005;
        proxy_http_version 1.1;
        proxy_set_header Host \$host;
        proxy_set_header X-Forwarded-Proto \$scheme;
    }
}
NGINX

ln -sf /etc/nginx/sites-available/vapp /etc/nginx/sites-enabled/vapp
rm -f /etc/nginx/sites-enabled/default
nginx -t
systemctl enable nginx
systemctl reload nginx
log "Nginx configured"

# --- 5) Admin Dockerfile (if not in repo yet) ---
if [[ ! -f "$FRONT_DIR/Dockerfile" ]]; then
  log "Creating Admin Dockerfile + nginx.conf"
  cat >"$FRONT_DIR/nginx.conf" <<'NGX'
server {
    listen 80;
    server_name _;
    root /usr/share/nginx/html;
    index index.html;
    gzip on;
    gzip_types text/plain text/css application/json application/javascript text/xml application/xml;
    location / { try_files $uri $uri/ /index.html; }
    location ~* \.(js|css|png|jpg|jpeg|gif|ico|svg|woff|woff2)$ {
        expires 7d;
        add_header Cache-Control "public, immutable";
    }
}
NGX
  cat >"$FRONT_DIR/Dockerfile" <<'DF'
FROM node:22-alpine AS build
WORKDIR /app
COPY package.json package-lock.json* ./
RUN npm ci
COPY . .
ARG VITE_API_URL=
ENV VITE_API_URL=$VITE_API_URL
RUN npm run build
FROM nginx:1.27-alpine AS final
COPY nginx.conf /etc/nginx/conf.d/default.conf
COPY --from=build /app/dist /usr/share/nginx/html
EXPOSE 80
CMD ["nginx", "-g", "daemon off;"]
DF
fi

# --- 6) API + SQL Server (Docker) ---
log "Starting API stack (first build ~5-10 min)..."
cd "$API_DIR"
docker compose -f docker/docker-compose.production.yml --env-file docker/.env up -d --build

log "Waiting for API health..."
api_code="000"
for i in $(seq 1 24); do
  sleep 10
  api_code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' http://127.0.0.1:8080/health 2>/dev/null || echo "000")"
  log "API health $i/24: $api_code"
  [[ "$api_code" == "200" ]] && break
done

# --- 7) Admin front ---
FRONT_DEPLOY_MODE="${FRONT_DEPLOY_MODE:-host}"
if [[ "$FRONT_DEPLOY_MODE" == "host" ]]; then
  log "Building Admin front on host (no Docker Hub)..."
  FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}" \
    SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/deploy-front-host.sh"
  front_code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo "000")"
else
  log "Building Admin front (Docker)..."
  cd "$FRONT_DIR"
  if docker build --build-arg VITE_API_URL= -t vapp-admin:latest .; then
    docker rm -f vapp-admin 2>/dev/null || true
    docker run -d --name vapp-admin -p 127.0.0.1:3005:80 --restart unless-stopped vapp-admin:latest
    front_code="000"
    for i in $(seq 1 12); do
      sleep 5
      front_code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' http://127.0.0.1:3005/ 2>/dev/null || echo "000")"
      log "Front health $i/12: $front_code"
      [[ "$front_code" == "200" ]] && break
    done
  else
    log "WARN: docker build failed — fallback to host build"
    FRONT_STATIC_ROOT="${FRONT_STATIC_ROOT:-/var/www/vapp-admin}" \
      SERVER_IP="$SERVER_IP" bash "$SCRIPT_DIR/deploy-front-host.sh"
    front_code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo "000")"
  fi
fi

public_code="$(curl -sS -m 10 -o /dev/null -w '%{http_code}' http://127.0.0.1/ 2>/dev/null || echo "000")"

echo ""
echo "================================================================================"
echo "  Vapp bootstrap finished"
echo "================================================================================"
echo "  API:     $api_code   http://${SERVER_IP}/health"
echo "  Admin:   $front_code   http://${SERVER_IP}/admin"
echo "  Swagger: http://${SERVER_IP}/swagger"
echo "  Nginx:   $public_code"
echo ""
echo "  Secrets: $SECRETS_FILE"
echo "  docker ps:"
docker ps --format 'table {{.Names}}\t{{.Status}}'
echo "================================================================================"

if [[ "$api_code" != "200" || "$front_code" != "200" ]]; then
  echo "WARN: some checks failed — wait 60s and run:"
  echo "  curl http://127.0.0.1:8080/health"
  echo "  docker logs --tail 50 vapp_api_prod"
  exit 1
fi
