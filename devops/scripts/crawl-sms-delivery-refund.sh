#!/usr/bin/env bash
# کراول کامل refund دلیوری + مالی + متن‌های کاربر
#
# Usage:
#   bash devops/scripts/crawl-sms-delivery-refund.sh
#   bash devops/scripts/crawl-sms-delivery-refund.sh finance
#   bash devops/scripts/crawl-sms-delivery-refund.sh flow
set -euo pipefail

ROOT="$(cd "$(dirname "$0")/../.." && pwd)"
cd "$ROOT"

MODE="${1:-all}"

echo "=== SMS delivery wallet-refund crawl (mode=$MODE) ==="
echo "Project: $ROOT"
echo

case "$MODE" in
  finance)
    FILTER='FullyQualifiedName~SmsDeliveryRefundCopyAndFinanceTests|FullyQualifiedName~SmsDeliveryRefundEligibilityTests'
    ;;
  flow)
    FILTER='FullyQualifiedName~SmsDeliveryWalletRefundFlowTests|FullyQualifiedName~SmsDeliveryRefundEligibilityTests'
    ;;
  *)
    FILTER='FullyQualifiedName~Api_Vapp.Tests.SmsDelivery'
    ;;
esac

dotnet test Tests/Api_Vapp.Tests.csproj \
  --filter "$FILTER" \
  --logger "console;verbosity=detailed" \
  --nologo

echo
echo "=== Crawl finished (mode=$MODE) ==="
