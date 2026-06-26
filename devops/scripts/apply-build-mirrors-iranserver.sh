#!/usr/bin/env bash
# Docker + NPM mirror ایران‌سرور — قبل از build روی سرور ایران
# Usage: sudo bash apply-build-mirrors-iranserver.sh
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

if [[ "${EUID:-$(id -u)}" -ne 0 ]]; then
  sudo bash "$0"
  exit $?
fi

bash "$SCRIPT_DIR/apply-docker-mirror-iranserver.sh"

if command -v npm >/dev/null 2>&1; then
  # رسمی: https://mirror.iranserver.com/ → Node.js NPM
  npm config set registry https://npm.iranserver.com/repository/npm/ 2>/dev/null || true
  npm config set strict-ssl false 2>/dev/null || true
  echo "OK: npm registry → npm.iranserver.com (official mirror)"
fi

if command -v dotnet >/dev/null 2>&1; then
  if ! dotnet nuget list source 2>/dev/null | grep -qi iranserver; then
    dotnet nuget add source "https://nuget.iranserver.com/repository/nuget/" -n iranserver 2>/dev/null || true
  fi
  echo "OK: dotnet nuget source iranserver"
fi

echo "OK: build mirrors ready (Docker + NPM + NuGet)"
