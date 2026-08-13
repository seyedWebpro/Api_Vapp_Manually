#!/usr/bin/env bash
# Pull production secrets from vapp-prod → Mac (local disaster recovery)
# Does NOT print secret values. Files stay chmod 600 / outside git where needed.
#
# Usage:
#   bash devops/scripts/pull-production-secrets.sh
#   SERVER=vapp-prod bash devops/scripts/pull-production-secrets.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
API_ROOT="$(cd "$SCRIPT_DIR/../.." && pwd)"
VAPP_ROOT="$(cd "$API_ROOT/.." && pwd)"
SCRAPE_ROOT="${SCRAPE_ROOT:-$VAPP_ROOT/scraping_Number_Vapp}"
SERVER="${SERVER:-vapp-prod}"
BACKUP_DIR="${BACKUP_DIR:-$HOME/vapp-local-secrets-backup}"
TS="$(date +%Y%m%d-%H%M%S)"

log() { echo "[$(date '+%Y-%m-%dT%H:%M:%S%z')] $*"; }
die() { echo "ERROR: $*" >&2; exit 1; }

command -v ssh >/dev/null || die "ssh not found"
command -v scp >/dev/null || die "scp not found"
command -v python3 >/dev/null || die "python3 not found"

ssh -o BatchMode=yes -o ConnectTimeout=15 "$SERVER" 'echo ok' >/dev/null \
  || die "Cannot SSH to $SERVER — fix ~/.ssh/config (alias vapp-prod)"

mkdir -p "$BACKUP_DIR/history" \
  "$API_ROOT/docker" \
  "$API_ROOT/secrets" \
  "$SCRAPE_ROOT"

log "Pulling secrets from $SERVER …"

# --- API docker/.env ---
scp -q "$SERVER:/root/Api_Vapp_Manually/docker/.env" "$API_ROOT/docker/.env"
chmod 600 "$API_ROOT/docker/.env"
cp -p "$API_ROOT/docker/.env" "$BACKUP_DIR/history/api-docker.env.$TS"

# --- short secrets file on server ---
if ssh -o BatchMode=yes "$SERVER" 'test -f /root/vapp-secrets.txt'; then
  scp -q "$SERVER:/root/vapp-secrets.txt" "$HOME/vapp-secrets.from-server.$TS.txt"
  chmod 600 "$HOME/vapp-secrets.from-server.$TS.txt"
  cp -p "$HOME/vapp-secrets.from-server.$TS.txt" "$BACKUP_DIR/history/vapp-secrets.from-server.$TS.txt"
fi

# --- scraper .env ---
scp -q "$SERVER:/root/scraping_Number_Vapp/.env" "$SCRAPE_ROOT/.env.production.server"
chmod 600 "$SCRAPE_ROOT/.env.production.server"
cp -p "$SCRAPE_ROOT/.env.production.server" "$BACKUP_DIR/history/scraping.env.$TS"

# --- firebase ---
if ssh -o BatchMode=yes "$SERVER" 'test -f /root/Api_Vapp_Manually/secrets/firebase-service-account.json'; then
  scp -q "$SERVER:/root/Api_Vapp_Manually/secrets/firebase-service-account.json" \
    "$API_ROOT/secrets/firebase-service-account.json"
  chmod 600 "$API_ROOT/secrets/firebase-service-account.json"
  cp -p "$API_ROOT/secrets/firebase-service-account.json" \
    "$BACKUP_DIR/history/firebase-service-account.json.$TS"
fi

# Ensure scraper production copy stays out of git
if [[ -f "$SCRAPE_ROOT/.gitignore" ]] && ! grep -qxF '.env.production.server' "$SCRAPE_ROOT/.gitignore"; then
  echo '.env.production.server' >> "$SCRAPE_ROOT/.gitignore"
fi

log "Assembling full local inventory (MerchantId, SMS, JWT, Scraper, …) …"

python3 - "$API_ROOT" "$SCRAPE_ROOT" "$BACKUP_DIR" "$TS" <<'PY'
import json, sys
from pathlib import Path
from datetime import datetime, timezone

api, scrape, backup, ts = map(Path, sys.argv[1:5])
home = Path.home()

def parse_env(path: Path):
    d = {}
    if not path.exists():
        return d
    for line in path.read_text().splitlines():
        s = line.strip()
        if not s or s.startswith("#") or "=" not in s:
            continue
        k, v = s.split("=", 1)
        d[k] = v
    return d

docker_env = parse_env(api / "docker/.env")
# Prefer newest from-server short file if present, else existing ~/vapp-secrets.txt
from_server = sorted(home.glob("vapp-secrets.from-server.*.txt"), reverse=True)
vapp_sec = parse_env(from_server[0]) if from_server else parse_env(home / "vapp-secrets.txt")
scrape_env = parse_env(scrape / ".env.production.server")
app = json.loads((api / "appsettings.json").read_text())

zp = app.get("ZarinPal", {})
sms = app.get("Sms", {})
jwt = app.get("Jwt", {})
ns = app.get("NumberScraperApi", {})
pp = app.get("PublicParticipant", {})
pay = app.get("Payment", {})
beh = pay.get("Behpardakht", {})

sa = docker_env.get("SA_PASSWORD") or vapp_sec.get("SA_PASSWORD", "")
jwt_docker = docker_env.get("Jwt__Secret", "")
jwt_sec = vapp_sec.get("Jwt__Secret", "")
scraper_key = (
    docker_env.get("SCRAPER_API_KEY")
    or vapp_sec.get("SCRAPER_API_KEY")
    or scrape_env.get("API_KEY", "")
)
scraper_sa = vapp_sec.get("SCRAPER_SA_PASSWORD") or scrape_env.get("SA_PASSWORD", "")
now = datetime.now(timezone.utc).astimezone().isoformat(timespec="seconds")

full = backup / "vapp-production-full.env"
full.write_text("\n".join([
    f"# Vapp production secrets — full local backup",
    f"# Pulled/assembled: {now}",
    f"# Source server: vapp-prod",
    f"# DO NOT commit. Keep chmod 600.",
    "",
    "# ═══ SQL / Docker (Api_Vapp_Manually/docker/.env) ═══",
    f"SA_PASSWORD={sa}",
    f"API_PORT_MAPPING={docker_env.get('API_PORT_MAPPING', '')}",
    f"PUBLIC_API_BASE_URL={docker_env.get('PUBLIC_API_BASE_URL', '')}",
    f"PUBLIC_FRONTEND_URL={docker_env.get('PUBLIC_FRONTEND_URL', '')}",
    f"FORM_PUBLIC_BASE_URL={docker_env.get('FORM_PUBLIC_BASE_URL', '')}",
    f"WHEEL_PUBLIC_BASE_URL={docker_env.get('WHEEL_PUBLIC_BASE_URL', '')}",
    f"CARD_PUBLIC_BASE_URL={docker_env.get('CARD_PUBLIC_BASE_URL', '')}",
    f"BOOKING_PUBLIC_BASE_URL={docker_env.get('BOOKING_PUBLIC_BASE_URL', '')}",
    f"Jwt__Secret={jwt_docker}",
    "",
    f"# Jwt.Secret in appsettings.json (fallback): {jwt.get('Secret', '')}",
    f"# Jwt__Secret historically in server vapp-secrets.txt: {jwt_sec}",
    "",
    "# ═══ Number Scraper ═══",
    f"SCRAPER_API_KEY={scraper_key}",
    f"SCRAPER_SA_PASSWORD={scraper_sa}",
    f"SCRAPER_API_KEY_REQUIRED={scrape_env.get('API_KEY_REQUIRED', '')}",
    f"SCRAPER_API_PORT_MAPPING={scrape_env.get('API_PORT_MAPPING', '')}",
    f"SCRAPER_DOTNET_WEBHOOK_URL={scrape_env.get('DOTNET_WEBHOOK_URL', '')}",
    f"SCRAPER_DOTNET_WEBHOOK_API_KEY={scrape_env.get('DOTNET_WEBHOOK_API_KEY', '')}",
    f"SCRAPER_DOTNET_WEBHOOK_ENABLED={scrape_env.get('DOTNET_WEBHOOK_ENABLED', '')}",
    f"SCRAPER_CORS_ORIGINS={scrape_env.get('CORS_ORIGINS', '')}",
    f"SCRAPER_API_DOCKER_IMAGE={scrape_env.get('API_DOCKER_IMAGE', '')}",
    f"SCRAPER_HEADLESS={scrape_env.get('SCRAPER_HEADLESS', '')}",
    f"SCRAPER_SPEED_MODE={scrape_env.get('SPEED_MODE', '')}",
    f"SCRAPE_QUEUE_MAX_SIZE={scrape_env.get('SCRAPE_QUEUE_MAX_SIZE', '')}",
    f"SCRAPE_TASK_TIMEOUT={scrape_env.get('SCRAPE_TASK_TIMEOUT', '')}",
    f"RATE_LIMIT_SCRAPE_MAX={scrape_env.get('RATE_LIMIT_SCRAPE_MAX', '')}",
    f"RATE_LIMIT_SCRAPE_WINDOW={scrape_env.get('RATE_LIMIT_SCRAPE_WINDOW', '')}",
    "",
    "# ═══ Payment / ZarinPal ═══",
    f"ZarinPal__MerchantId={zp.get('MerchantId', '')}",
    f"ZarinPal__CallbackUrl={zp.get('CallbackUrl', '')}",
    f"ZarinPal__Sandbox={zp.get('Sandbox', '')}",
    f"ZarinPal__Currency={zp.get('Currency', 'IRT')}",
    f"ZarinPal__AppReturnUrl={zp.get('AppReturnUrl', 'vapp://payment/result')}",
    f"Payment__UseSimulation={pay.get('UseSimulation', '')}",
    f"Payment__ApiBaseUrl={pay.get('ApiBaseUrl', '')}",
    f"Payment__Behpardakht__TerminalId={beh.get('TerminalId', '')}",
    f"Payment__Behpardakht__Username={beh.get('Username', '')}",
    f"Payment__Behpardakht__Password={beh.get('Password', '')}",
    "",
    "# ═══ SMS ═══",
    f"Sms__ApiKey={sms.get('ApiKey', '')}",
    f"Sms__BaseUrl={sms.get('BaseUrl', '')}",
    f"Sms__SenderNumber={sms.get('SenderNumber', '')}",
    "",
    "# ═══ Other ═══",
    f"Jwt__Issuer={jwt.get('Issuer', '')}",
    f"Jwt__Audience={jwt.get('Audience', '')}",
    f"PublicParticipant__TokenPepper={pp.get('TokenPepper', '')}",
    f"NumberScraperApi__Enabled={ns.get('Enabled', '')}",
    f"NumberScraperApi__BaseUrl={ns.get('BaseUrl', '')}",
    f"NumberScraperApi__ApiKey_appsettings={ns.get('ApiKey', '')}",
]) + "\n")
full.chmod(0o600)

short = home / "vapp-secrets.txt"
short.write_text("\n".join([
    f"# Vapp production secrets — {now}",
    "# نگه دارید — برای بازیابی رمز DB، JWT، Scraper، Merchant، SMS",
    f"SA_PASSWORD={sa}",
    f"Jwt__Secret={jwt_docker}",
    f"Jwt__Secret_appsettings={jwt.get('Secret', '')}",
    f"SCRAPER_API_KEY={scraper_key}",
    f"SCRAPER_SA_PASSWORD={scraper_sa}",
    f"ZarinPal__MerchantId={zp.get('MerchantId', '')}",
    f"Sms__ApiKey={sms.get('ApiKey', '')}",
    f"Sms__SenderNumber={sms.get('SenderNumber', '')}",
    f"Sms__BaseUrl={sms.get('BaseUrl', '')}",
    f"PublicParticipant__TokenPepper={pp.get('TokenPepper', '')}",
    f"PUBLIC_API_BASE_URL={docker_env.get('PUBLIC_API_BASE_URL', '')}",
    f"FORM_PUBLIC_BASE_URL={docker_env.get('FORM_PUBLIC_BASE_URL', '')}",
    f"WHEEL_PUBLIC_BASE_URL={docker_env.get('WHEEL_PUBLIC_BASE_URL', '')}",
]) + "\n")
short.chmod(0o600)

# Enrich local docker/.env with scraper keys if missing (restore-ready)
env_path = api / "docker/.env"
text = env_path.read_text()
if "SCRAPER_API_KEY=" not in text and scraper_key:
    if not text.endswith("\n"):
        text += "\n"
    text += (
        "\n# Number Scraper (from server secrets / scraping .env)\n"
        f"SCRAPER_API_KEY={scraper_key}\n"
        "NumberScraperApi__Enabled=true\n"
        "NumberScraperApi__BaseUrl=http://host.docker.internal:8000\n"
        "NumberScraperApi__ApiKey=${SCRAPER_API_KEY}\n"
        "NumberScraperApi__TimeoutSeconds=120\n"
    )
    env_path.write_text(text)
    env_path.chmod(0o600)

readme = backup / "README.md"
readme.write_text(f"""# Vapp local secrets backup

آخرین همگام‌سازی: `{now}` (run `{ts}`)

## فایل‌ها

| فایل | محتوا |
|------|--------|
| `~/vapp-secrets.txt` | خلاصه بحرانی: DB, JWT, Scraper, MerchantId, SMS |
| `~/vapp-local-secrets-backup/vapp-production-full.env` | بکاپ کامل |
| `Api_Vapp_Manually/docker/.env` | کپی `docker/.env` سرور |
| `scraping_Number_Vapp/.env.production.server` | کپی `.env` ربات |
| `Api_Vapp_Manually/secrets/firebase-service-account.json` | Firebase |
| `history/` | snapshot تاریخ‌دار |

```bash
cd ~/Documents/javad_project/vapp/Api_Vapp_Manually
bash devops/scripts/pull-production-secrets.sh
```
""")
print("OK MerchantId=", zp.get("MerchantId"))
print("OK full=", full)
print("OK short=", short)
PY

log "Done."
log "  ~/vapp-secrets.txt"
log "  ~/vapp-local-secrets-backup/vapp-production-full.env"
log "  $API_ROOT/docker/.env"
log "  $SCRAPE_ROOT/.env.production.server"
log "  $API_ROOT/secrets/firebase-service-account.json"
log "  history → $BACKUP_DIR/history/"
