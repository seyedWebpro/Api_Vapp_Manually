#!/usr/bin/env bash
# Resolve DOMAIN_HOST for apply-nginx.sh
#
# Rules:
#   - DOMAIN_HOST unset → use DOMAIN from server.conf when Let's Encrypt cert exists
#   - DOMAIN_HOST="" explicitly → IP-only (switch-to-domain.sh --ip-only)
#   - DOMAIN_HOST set to a name → use as-is
#
# Source after load-server-conf.sh. Sets DOMAIN_HOST in the caller's shell.
resolve_nginx_domain_host() {
  if [[ -n "${DOMAIN_HOST+x}" ]]; then
    return 0
  fi
  local candidate="${DOMAIN:-}"
  if [[ -n "$candidate" && -f "/etc/letsencrypt/live/${candidate}/fullchain.pem" ]]; then
    DOMAIN_HOST="$candidate"
    echo "INFO: auto DOMAIN_HOST=$DOMAIN_HOST (Let's Encrypt cert present)"
  else
    DOMAIN_HOST=""
  fi
}
