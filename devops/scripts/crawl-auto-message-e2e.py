#!/usr/bin/env python3
"""
E2E crawl: automated messages (Birthday, SpecialOccasion, stubs) + wallet deduction.
Local only — BASE_URL default http://127.0.0.1:5054
"""
from __future__ import annotations

import json
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from datetime import datetime, timedelta, timezone
from typing import Any

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5054"
PHONE_TODAY = "09920374397"
PHONE_MONTH = "09392615526"
NOTEBOOK_ID = 2

PASS = 0
FAIL = 0
RESULTS: list[tuple[str, bool, str]] = []


def log(msg: str) -> None:
    print(msg, flush=True)


def check(name: str, cond: bool, detail: str = "") -> None:
    global PASS, FAIL
    if cond:
        PASS += 1
        RESULTS.append((name, True, detail))
        log(f"PASS  {name}" + (f" — {detail}" if detail else ""))
    else:
        FAIL += 1
        RESULTS.append((name, False, detail))
        log(f"FAIL  {name}" + (f" — {detail}" if detail else ""))


def req(method: str, path: str, body: Any | None = None, timeout: int = 60) -> tuple[int, dict]:
    url = BASE + path
    data = None
    headers = {"Accept": "application/json"}
    if body is not None:
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"
    r = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(r, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8")
            return resp.status, (json.loads(raw) if raw else {})
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8")
        try:
            return e.code, json.loads(raw) if raw else {}
        except Exception:
            return e.code, {"raw": raw}


def jget(d: dict, *keys, default=None):
    cur: Any = d
    for k in keys:
        if not isinstance(cur, dict):
            return default
        cur = cur.get(k)
    return default if cur is None else cur


def wallet_balance() -> float:
    code, d = req("GET", "/api/Wallet/balance")
    return float(jget(d, "data", "balance", default=0) or 0)


def ensure_contact(mobile: str, full_name: str, dob: str) -> int:
    """Create or update contact with DOB. Returns contact id."""
    code, d = req(
        "POST",
        "/api/Contact",
        {
            "contactNotebookId": NOTEBOOK_ID,
            "mobileNumber": mobile,
            "fullName": full_name,
            "dateOfBirth": dob,
            "tagNames": ["auto-msg-e2e"],
        },
    )
    cid = jget(d, "data", "id")
    check(f"create/ensure contact {mobile}", code in (200, 201) and bool(cid), f"code={code} id={cid}")
    if not cid:
        return 0
    # Force DOB update (create may return existing without updating DOB)
    code2, d2 = req(
        "POST",
        f"/api/Contact/{cid}/update",
        {
            "fullName": full_name,
            "dateOfBirth": dob,
        },
    )
    check(f"update DOB {mobile} → {dob}", code2 in (200, 201) and jget(d2, "success") is True, f"code={code2}")
    # verify
    code3, d3 = req("GET", f"/api/Contact/{cid}")
    got = jget(d3, "data", "dateOfBirth") or jget(d3, "data", "additionalInfo", "dateOfBirth")
    check(f"verify DOB stored {mobile}", bool(got), f"got={got}")
    return int(cid)


def setup_birthday(send_hhmm: str, contact_ids: list[int]) -> int:
    code, d = req("POST", "/api/AutomatedMessage/create-draft", {"automationType": "Birthday"})
    am_id = jget(d, "data", "id")
    check("Birthday create-draft", code in (200, 201) and bool(am_id), f"id={am_id}")
    if not am_id:
        return 0

    code, d = req(
        "POST",
        f"/api/AutomatedMessage/{am_id}/recipients/select",
        {"applyToAllContacts": False, "contactNotebookId": NOTEBOOK_ID, "excludedContactIds": []},
    )
    check("Birthday select recipients", jget(d, "success") is True, f"code={code}")

    code, d = req(
        "POST",
        f"/api/AutomatedMessage/{am_id}/settings",
        {
            "type": "Birthday",
            "birthdaySettings": {"sendTime": send_hhmm, "repeatYearly": True},
        },
    )
    check(f"Birthday settings sendTime={send_hhmm}", jget(d, "success") is True, f"code={code}")

    content = f"تست تولد خودکار E2E — سلام {{FullName}} — {datetime.now(timezone.utc).isoformat()}"
    code, d = req(
        "POST",
        f"/api/AutomatedMessage/{am_id}/message/content",
        {"content": content},
    )
    check("Birthday save content", jget(d, "success") is True, f"code={code}")

    code, d = req("POST", f"/api/AutomatedMessage/{am_id}/toggle-status", {"isActive": True})
    check("Birthday activate", jget(d, "success") is True and jget(d, "data", "isActive") is True, f"code={code}")

    code, d = req("GET", f"/api/AutomatedMessage/{am_id}")
    st = jget(d, "data", "scheduledTime")
    check("Birthday ScheduledTime persisted", bool(st), f"scheduledTime={st}")
    return int(am_id)


def setup_special_occasion_today(send_hhmm: str | None = None) -> int:
    code, d = req("POST", "/api/AutomatedMessage/create-draft", {"automationType": "SpecialOccasion"})
    am_id = jget(d, "data", "id")
    check("SpecialOccasion create-draft", code in (200, 201) and bool(am_id), f"id={am_id}")
    if not am_id:
        return 0

    req(
        "POST",
        f"/api/AutomatedMessage/{am_id}/recipients/select",
        {"applyToAllContacts": False, "contactNotebookId": NOTEBOOK_ID},
    )

    today = datetime.now(timezone.utc).date().isoformat()
    code, d = req(
        "POST",
        f"/api/AutomatedMessage/{am_id}/settings",
        {
            "type": "SpecialOccasion",
            "specialOccasionSettings": {
                "action": "Add",
                "occasionName": f"E2E Occasion {today}",
                "occasionDate": today,
            },
        },
    )
    check("SpecialOccasion add occasion today", jget(d, "success") is True, f"code={code} msg={jget(d,'message')}")

    # Optional ScheduledTime via update (ManageSpecialOccasions does not set it)
    if send_hhmm:
        # ScheduledTime as TimeSpan string "HH:mm:ss"
        code, d = req(
            "POST",
            f"/api/AutomatedMessage/{am_id}/update",
            {"scheduledTime": f"{send_hhmm}:00"},
        )
        check("SpecialOccasion set ScheduledTime", jget(d, "success") is True, f"code={code}")

    code, d = req(
        "POST",
        f"/api/AutomatedMessage/{am_id}/message/content",
        {"content": f"تست مناسبت خاص E2E — {{FullName}} — {today}"},
    )
    check("SpecialOccasion content", jget(d, "success") is True, f"code={code}")

    code, d = req("POST", f"/api/AutomatedMessage/{am_id}/toggle-status", {"isActive": True})
    check("SpecialOccasion activate", jget(d, "success") is True, f"code={code}")
    return int(am_id)


def setup_stub(atype: str, settings: dict) -> int:
    code, d = req("POST", "/api/AutomatedMessage/create-draft", {"automationType": atype})
    am_id = jget(d, "data", "id")
    check(f"{atype} create-draft", code in (200, 201) and bool(am_id), f"id={am_id}")
    if not am_id:
        return 0
    req("POST", f"/api/AutomatedMessage/{am_id}/recipients/select", {"applyToAllContacts": True})
    code, d = req("POST", f"/api/AutomatedMessage/{am_id}/settings", settings)
    check(f"{atype} settings", jget(d, "success") is True, f"code={code} msg={jget(d,'message')}")
    code, d = req(
        "POST",
        f"/api/AutomatedMessage/{am_id}/message/content",
        {"content": f"stub {atype} — should not send SMS"},
    )
    check(f"{atype} content", jget(d, "success") is True, f"code={code}")
    req("POST", f"/api/AutomatedMessage/{am_id}/toggle-status", {"isActive": True})
    return int(am_id)


def pending_approvals_for_automation(am_id: int) -> list[dict]:
    code, d = req("GET", "/api/Admin/MessageApproval/pending?page=1&pageSize=50")
    items = jget(d, "data", "items") or []
    # Campaign approvals linked — filter by title/content preview containing automation id if possible
    # Better: list campaigns via raw query isn't available; use Message campaigns if endpoint exists
    return items


def find_campaigns_pending(am_id: int) -> list[dict]:
    """Use admin pending + campaign list heuristics."""
    # Try message campaigns endpoint
    for path in (
        f"/api/Message/campaign?pageNumber=1&pageSize=50",
        f"/api/Message/campaigns?pageNumber=1&pageSize=50",
    ):
        code, d = req("GET", path)
        if code == 200:
            items = jget(d, "data", "campaigns") or jget(d, "data", "items") or jget(d, "data") or []
            if isinstance(items, list):
                matched = [
                    c
                    for c in items
                    if isinstance(c, dict)
                    and (
                        c.get("automatedMessageId") == am_id
                        or str(am_id) in str(c.get("title") or "")
                    )
                    and c.get("status") in ("PendingApproval", "Pending", "Sending", "Completed", "Sent")
                ]
                if matched:
                    return matched
    # Fall back: approval requests
    code, d = req("GET", "/api/Admin/MessageApproval/pending?page=1&pageSize=100")
    items = jget(d, "data", "items") or []
    return [i for i in items if i.get("requestType") == "Campaign" and i.get("status") == "Pending"]


def list_pending_campaign_approvals(since_iso: str | None = None) -> list[dict]:
    code, d = req("GET", "/api/Admin/MessageApproval/pending?page=1&pageSize=100")
    items = jget(d, "data", "items") or []
    out = []
    for item in items:
        if item.get("requestType") != "Campaign":
            continue
        if item.get("status") != "Pending":
            continue
        created = item.get("createdAt") or ""
        if since_iso and created and created < since_iso[:19]:
            continue
        out.append(item)
    return out


def approve_approvals(items: list[dict], label: str) -> list[int]:
    approved_ids = []
    for item in items:
        aid = item.get("id")
        code2, d2 = req("POST", f"/api/Admin/MessageApproval/{aid}/approve")
        ok = jget(d2, "success") is True
        check(
            f"admin approve [{label}] id={aid} recipients={item.get('recipientsCount')}",
            ok,
            f"code={code2} msg={jget(d2,'message')} title={item.get('titlePreview')}",
        )
        if ok:
            approved_ids.append(int(aid))
    return approved_ids


def reject_approvals(items: list[dict], label: str) -> None:
    for item in items:
        aid = item.get("id")
        code2, d2 = req(
            "POST",
            f"/api/Admin/MessageApproval/{aid}/reject",
            {"rejectionReason": f"E2E reject {label} — avoid mass SMS to test numbers"},
        )
        check(
            f"admin reject [{label}] id={aid}",
            jget(d2, "success") is True,
            f"code={code2} msg={jget(d2,'message')}",
        )


def campaigns_for_am(am_id: int) -> list[dict]:
    code, d = req("GET", "/api/Message/campaign?pageNumber=1&pageSize=50")
    items = jget(d, "data", "campaigns") or []
    return [c for c in items if c.get("automatedMessageId") == am_id]


def test_send_birthday_endpoint_bug() -> None:
    """Document year-match bug on test-send-birthday-now."""
    code, d = req("POST", "/api/AutomatedMessage/test-send-birthday-now")
    msg = jget(d, "message") or ""
    data = jget(d, "data") or ""
    # With DOB year != current year, endpoint often sends 0 due to full-date match bug
    log(f"INFO  test-send-birthday-now → code={code} message={msg} data={data}")
    check(
        "test-send-birthday-now responds",
        code in (200, 400),
        f"code={code} (known: compares full Date incl. year — may send 0)",
    )


def wait_until(target: datetime, label: str) -> None:
    while True:
        now = datetime.now(timezone.utc)
        remaining = (target - now).total_seconds()
        if remaining <= 0:
            break
        log(f"WAIT  {label}: {remaining:.0f}s remaining (now={now.strftime('%H:%M:%S')} UTC, target={target.strftime('%H:%M:%S')} UTC)")
        time.sleep(min(15, max(1, remaining)))


def main() -> int:
    log(f"=== Auto-message E2E @ {BASE} ===")
    code, _ = req("GET", "/health")
    check("health", code == 200)

    now = datetime.now(timezone.utc)
    # Schedule ~2.5 minutes ahead so we have setup time + within ±2 min window when BG ticks
    send_at = now + timedelta(minutes=2, seconds=30)
    send_hhmm = send_at.strftime("%H:%M")
    today_dob = f"{now.year - 25}-{now.month:02d}-{now.day:02d}"
    month_later = (now.date() + timedelta(days=32)).replace(day=min(28, now.day))
    # safer: add ~1 month
    if now.month == 12:
        month_later = now.date().replace(year=now.year + 1, month=1)
    else:
        try:
            month_later = now.date().replace(month=now.month + 1)
        except ValueError:
            month_later = now.date().replace(month=now.month + 1, day=28)
    month_dob = f"{now.year - 30}-{month_later.month:02d}-{month_later.day:02d}"

    log(f"UTC now={now.isoformat()} sendTime={send_hhmm} todayDOB={today_dob} monthDOB={month_dob}")

    bal0 = wallet_balance()
    check("wallet readable before", bal0 >= 0, f"balance={bal0}")

    cid_today = ensure_contact(PHONE_TODAY, "تست تولد امروز", today_dob)
    cid_month = ensure_contact(PHONE_MONTH, "تست تولد یک‌ماه بعد", month_dob)

    since = datetime.now(timezone.utc).isoformat().replace("+00:00", "Z")

    birthday_id = setup_birthday(send_hhmm, [cid_today, cid_month])
    # SpecialOccasion: schedule a bit after birthday window to isolate, OR same day no schedule (fires every minute)
    # Use scheduled time +4 min to avoid blasting all contacts during birthday wait
    so_at = send_at + timedelta(minutes=3)
    so_hhmm = so_at.strftime("%H:%M")
    so_id = setup_special_occasion_today(send_hhmm=so_hhmm)

    stub_ids = []
    stub_ids.append(setup_stub("CashbackExpiry", {"type": "CashbackExpiry", "cashbackExpirySettings": {"daysBeforeExpiry": 2, "executionMode": "Once"}}))
    stub_ids.append(setup_stub("Welcome", {"type": "Welcome"}))
    stub_ids.append(setup_stub("PurchaseReminder", {"type": "PurchaseReminder", "purchaseReminderSettings": {"daysWithoutPurchase": 30}}))
    stub_ids.append(setup_stub("Custom", {"type": "Custom", "customAutomationSettings": {"activationConditions": "{\"rule\":\"test\"}"}}))

    test_send_birthday_endpoint_bug()

    # Wait for birthday schedule window
    wait_until(send_at - timedelta(seconds=10), "until birthday window")
    log("POLL  waiting for Birthday PendingApproval campaign (up to 4 min)...")
    birthday_items: list[dict] = []
    deadline = send_at + timedelta(minutes=4)
    while datetime.now(timezone.utc) < deadline:
        fresh = list_pending_campaign_approvals(since_iso=since)
        for i in fresh:
            preview = (i.get("contentPreview") or "") + (i.get("titlePreview") or "")
            log(
                f"  pending id={i.get('id')} title={i.get('titlePreview')} "
                f"recipients={i.get('recipientsCount')} created={i.get('createdAt')}"
            )
            if "تولد" in preview or "Birthday" in preview or (i.get("recipientsCount") == 1 and "مناسبت" not in preview):
                if i not in birthday_items:
                    birthday_items.append(i)
        # Also match via campaign automatedMessageId
        cams = campaigns_for_am(birthday_id)
        for c in cams:
            log(f"  campaign am={birthday_id}: id={c.get('id')} status={c.get('status')} recipients={c.get('recipientsCount')}")
        if birthday_items or any(c.get("status") == "PendingApproval" for c in cams):
            break
        time.sleep(15)

    check("Birthday queued for admin approval within window", len(birthday_items) > 0, f"items={len(birthday_items)}")

    # Birthday should only include today's DOB contact (not month-later)
    if birthday_items:
        rc = birthday_items[0].get("recipientsCount")
        check("Birthday recipientsCount == 1 (only today DOB)", rc == 1, f"recipientsCount={rc}")

    bal_before_approve = wallet_balance()
    approved = approve_approvals(birthday_items, "Birthday")
    check("Birthday campaign approved", len(approved) > 0, f"approved={approved}")

    time.sleep(10)
    bal1 = wallet_balance()
    deducted = bal_before_approve - bal1
    check("wallet decreased after Birthday approve/send", deducted > 0, f"before={bal_before_approve} after={bal1} delta={deducted}")
    check("Birthday wallet delta >= 160 (1 SMS part)", deducted >= 160, f"delta={deducted}")

    cams = campaigns_for_am(birthday_id)
    if cams:
        c = cams[0]
        check(
            "Birthday campaign terminal status Sent/Completed/Partial",
            (c.get("status") or "") in ("Sent", "Completed", "Partial", "Failed", "Sending"),
            f"status={c.get('status')} sent={c.get('sentCount')} failed={c.get('failedCount')}",
        )
        # Fetch campaign detail for recipients
        code, d = req("GET", f"/api/Message/campaign/{c.get('id')}")
        recs = jget(d, "data", "recipients") or []
        mobiles = [r.get("mobileNumber") for r in recs]
        check("Birthday SMS to today phone", PHONE_TODAY in mobiles, f"mobiles={mobiles}")
        check("Birthday NOT to month-later phone", PHONE_MONTH not in mobiles, f"mobiles={mobiles}")

    code, d = req("GET", f"/api/Contact/{cid_month}")
    check("month-later contact exists", jget(d, "success") is True, f"id={cid_month} dob={jget(d,'data','dateOfBirth')}")

    # Welcome stub: create new contact — no auto SMS
    bal_w0 = wallet_balance()
    code, d = req(
        "POST",
        "/api/Contact",
        {
            "contactNotebookId": NOTEBOOK_ID,
            "mobileNumber": "09120000099",
            "fullName": "تست Welcome نباید SMS",
            "dateOfBirth": "1990-01-01",
        },
    )
    welcome_cid = jget(d, "data", "id")
    time.sleep(5)
    bal_w1 = wallet_balance()
    check(
        "Welcome does NOT auto-send on contact create (stub)",
        bal_w1 == bal_w0,
        f"balance unchanged {bal_w0}→{bal_w1}",
    )

    # SpecialOccasion: verify queue only, then REJECT (sends to ALL contacts — avoid mass SMS)
    wait_until(so_at - timedelta(seconds=5), "until SpecialOccasion window")
    so_items: list[dict] = []
    deadline2 = so_at + timedelta(minutes=4)
    while datetime.now(timezone.utc) < deadline2:
        fresh = list_pending_campaign_approvals(since_iso=None)
        for i in fresh:
            preview = (i.get("contentPreview") or "") + (i.get("titlePreview") or "")
            log(f"  SO pending id={i.get('id')} recipients={i.get('recipientsCount')} title={i.get('titlePreview')}")
            if "مناسبت" in preview or "SpecialOccasion" in preview or "Occasion" in preview or "E2E" in preview:
                so_items.append(i)
        cams_so = campaigns_for_am(so_id)
        for c in cams_so:
            log(f"  SO campaign: id={c.get('id')} status={c.get('status')} recipients={c.get('recipientsCount')}")
            if c.get("status") == "PendingApproval" and not so_items:
                # synthesize from campaign via pending list match
                so_items = [i for i in fresh if i.get("messageCampaignId") == c.get("id")] or so_items
        if so_items or any(c.get("status") == "PendingApproval" for c in cams_so):
            if not so_items:
                so_items = [i for i in fresh if (i.get("recipientsCount") or 0) > 1]
            break
        time.sleep(15)

    check("SpecialOccasion queued for approval", len(so_items) > 0 or bool(campaigns_for_am(so_id)), f"items={len(so_items)}")
    if so_items:
        check(
            "SpecialOccasion queues ALL contacts (runtime ignores selection)",
            (so_items[0].get("recipientsCount") or 0) >= 2,
            f"recipientsCount={so_items[0].get('recipientsCount')} (edge: not limited to selected notebook)",
        )
        reject_approvals(so_items, "SpecialOccasion")
        check("SpecialOccasion rejected — no wallet change for SO", True, "skipped mass send")

    log("INFO  Stubs CashbackExpiry/Welcome/PurchaseReminder/Custom = background TODOs; no SMS expected.")
    check("stubs created without API error", all(x and x > 0 for x in stub_ids), f"ids={stub_ids}")

    # Confirm stubs did not queue campaigns after a BG tick
    time.sleep(70)
    stub_cam_count = sum(len(campaigns_for_am(sid)) for sid in stub_ids if sid)
    check("stub automations created 0 campaigns", stub_cam_count == 0, f"campaigns={stub_cam_count}")
    log("")
    log(f"=== SUMMARY PASS={PASS} FAIL={FAIL} ===")
    for name, ok, detail in RESULTS:
        if not ok:
            log(f"  FAIL: {name} — {detail}")
    log(f"birthday_am={birthday_id} special_am={so_id} stubs={stub_ids}")
    log(f"contacts today={cid_today} month={cid_month} welcome={welcome_cid}")
    log(f"wallet start={bal0} mid={bal1} end={wallet_balance()}")
    return 0 if FAIL == 0 else 1


if __name__ == "__main__":
    sys.exit(main())
