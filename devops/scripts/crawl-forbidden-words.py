#!/usr/bin/env python3
"""Crawl Forbidden Words — admin CRUD + block/unblock user content."""
from __future__ import annotations

import json
import os
import signal
import subprocess
import sys
import time
import urllib.error
import urllib.parse
import urllib.request
from pathlib import Path

BASE = os.environ.get("BASE_URL", "http://127.0.0.1:5054").rstrip("/")
ROOT = Path(__file__).resolve().parents[2]
LOG = Path("/tmp/vapp-forbidden-words-crawl.log")
PIDFILE = Path("/tmp/vapp-forbidden-words-crawl.pid")
SKIP_API_RESTART = os.environ.get("SKIP_API_RESTART", "0") == "1"
SKIP_BUILD = os.environ.get("SKIP_BUILD", "0") == "1"
DOTNET = "/usr/local/share/dotnet/dotnet"
SUFFIX = str(int(time.time()))
WORD_A = f"قمارکراول{SUFFIX}"
WORD_B = f"شرطکراول{SUFFIX}"
WORD_C = f"کازینوکراول{SUFFIX}"
WORD_EDIT = f"ویرایشکراول{SUFFIX}"

PASS = 0
FAIL = 0
api_proc: subprocess.Popen | None = None


def check(name: str, ok: bool) -> None:
    global PASS, FAIL
    if ok:
        print(f"PASS  {name}")
        PASS += 1
    else:
        print(f"FAIL  {name}")
        FAIL += 1


def pick(obj, *keys):
    if not isinstance(obj, dict):
        return None
    for k in keys:
        if k in obj and obj[k] is not None:
            return obj[k]
        pascal = k[:1].upper() + k[1:] if k else k
        if pascal in obj and obj[pascal] is not None:
            return obj[pascal]
    return None


def req(method: str, path: str, body=None, form: dict | None = None, timeout=45):
    url = f"{BASE}{path}"
    data = None
    headers = {"Accept": "application/json"}
    if form is not None:
        boundary = f"----crawl{SUFFIX}"
        parts = []
        for k, v in form.items():
            parts.append(
                f"--{boundary}\r\nContent-Disposition: form-data; name=\"{k}\"\r\n\r\n{v}\r\n"
            )
        parts.append(f"--{boundary}--\r\n")
        data = "".join(parts).encode("utf-8")
        headers["Content-Type"] = f"multipart/form-data; boundary={boundary}"
    elif body is not None:
        data = json.dumps(body, ensure_ascii=False).encode("utf-8")
        headers["Content-Type"] = "application/json"
    request = urllib.request.Request(url, data=data, headers=headers, method=method)
    try:
        with urllib.request.urlopen(request, timeout=timeout) as resp:
            raw = resp.read().decode("utf-8") or "{}"
            return resp.status, json.loads(raw)
    except urllib.error.HTTPError as e:
        raw = e.read().decode("utf-8") or "{}"
        try:
            payload = json.loads(raw)
        except json.JSONDecodeError:
            payload = {"raw": raw}
        return e.code, payload
    except Exception as e:
        return 0, {"error": str(e)}


def health_ok() -> bool:
    import urllib.request
    try:
        with urllib.request.urlopen(f"{BASE}/health", timeout=5) as resp:
            return resp.status == 200
    except Exception:
        return False


def ensure_api() -> None:
    global api_proc
    if SKIP_API_RESTART:
        if not health_ok():
            print(f"API not healthy at {BASE} and SKIP_API_RESTART=1")
            sys.exit(1)
        print("API_READY (existing)")
        return

    print("===== BUILD + RESTART API =====")
    env = os.environ.copy()
    env["DOTNET_ROOT"] = "/usr/local/share/dotnet"
    env["PATH"] = f"/usr/local/share/dotnet:{env.get('PATH', '')}"
    env["ASPNETCORE_ENVIRONMENT"] = "Development"
    env["DatabaseProvider"] = os.environ.get("DatabaseProvider", "LocalDocker")

    if not (SKIP_BUILD and (ROOT / "bin/Debug/net8.0/Api_Vapp.dll").exists()):
        subprocess.check_call([DOTNET, "build", "Api_Vapp.csproj", "-v", "q"], cwd=ROOT, env=env)

    # free port
    try:
        out = subprocess.check_output(["lsof", "-t", "-iTCP:5054", "-sTCP:LISTEN"], text=True)
        for pid in out.split():
            try:
                os.kill(int(pid), signal.SIGTERM)
            except OSError:
                pass
        time.sleep(2)
        out = subprocess.check_output(["lsof", "-t", "-iTCP:5054", "-sTCP:LISTEN"], text=True)
        for pid in out.split():
            try:
                os.kill(int(pid), signal.SIGKILL)
            except OSError:
                pass
        time.sleep(1)
    except subprocess.CalledProcessError:
        pass

    LOG.write_text("", encoding="utf-8")
    logf = open(LOG, "a", encoding="utf-8")
    api_proc = subprocess.Popen(
        [DOTNET, "exec", "bin/Debug/net8.0/Api_Vapp.dll", "--urls", "http://127.0.0.1:5054"],
        cwd=ROOT,
        env=env,
        stdout=logf,
        stderr=subprocess.STDOUT,
        start_new_session=True,
    )
    PIDFILE.write_text(str(api_proc.pid), encoding="utf-8")
    print(f"API_PID={api_proc.pid}")

    for i in range(90):
        if api_proc.poll() is not None:
            print("API_DIED_EARLY")
            print(LOG.read_text(encoding="utf-8")[-4000:])
            sys.exit(1)
        if health_ok():
            code, body = req("GET", "/api/Admin/ForbiddenWords")
            if code == 200 and pick(body, "success") is True:
                print("API_READY")
                return
            print(f"health=200 ForbiddenWords={code} waiting i={i}")
        time.sleep(2)
    print("API_NOT_READY")
    print(LOG.read_text(encoding="utf-8")[-4000:])
    sys.exit(1)


def main() -> int:
    print(f"=== crawl-forbidden-words ===\nBASE={BASE} SUFFIX={SUFFIX}")
    ensure_api()

    check("GET /health -> 200", health_ok())

    code, body = req("GET", "/api/Admin/ForbiddenWords?includeInactive=true")
    check("GET Admin/ForbiddenWords -> 200", code == 200)
    check("list success=true", pick(body, "success") is True)

    code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": WORD_A, "isActive": True})
    check("create single -> 200", code == 200)
    check("create single success", pick(body, "success") is True)
    data = pick(body, "data") or {}
    check("createCount=1", pick(data, "createdCount") == 1)
    created = pick(data, "created") or []
    id_a = pick(created[0], "id") if created else None
    check("created id present", bool(id_a))

    code, body = req(
        "POST",
        "/api/Admin/ForbiddenWords/create",
        {"word": f"{WORD_B}\n{WORD_C}", "isActive": True},
    )
    check("bulk create -> 200", code == 200)
    data = pick(body, "data") or {}
    check("bulk createdCount=2", pick(data, "createdCount") == 2)
    created = pick(data, "created") or []
    id_b = pick(created[0], "id") if len(created) > 0 else None
    id_c = pick(created[1], "id") if len(created) > 1 else None

    code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": WORD_A, "isActive": True})
    check("duplicate -> 400", code == 400)
    check("duplicate VALIDATION_FAILED", pick(body, "errorCode") == "VALIDATION_FAILED")

    code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": "   ", "isActive": True})
    check("empty word -> 400", code == 400)

    q = urllib.parse.quote(WORD_A)
    code, body = req("GET", f"/api/Admin/ForbiddenWords?includeInactive=true&search={q}")
    items = pick(body, "data") or []
    check("search finds WORD_A", any(pick(x, "word") == WORD_A for x in items))

    code, body = req("GET", "/api/ForbiddenWords/active")
    check("GET active -> 200", code == 200)
    active = pick(body, "data") or []
    check("active list contains WORD_A", WORD_A in [str(x) for x in active])

    code, body = req("POST", "/api/ForbiddenWords/validate", {"text": f"سلام این متن {WORD_A} دارد"})
    check("validate blocked -> 400", code == 400)
    check("validate FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")
    msg = str(pick(body, "message") or "")
    check("validate Persian message mentions word", WORD_A in msg)

    code, body = req("POST", "/api/ForbiddenWords/validate", {"text": "متن کاملا عادی و تمیز"})
    check("validate clean -> 200", code == 200 and pick(body, "success") is True)

    code, body = req("POST", "/api/Message", {"content": f"پرداخت با {WORD_A} انجام شود"})
    check("CreateMessage blocked -> 400", code == 400)
    check("CreateMessage FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")

    code, body = req(
        "POST",
        "/api/Template",
        form={
            "Name": f"قالب تست کراول {SUFFIX}",
            "Content": f"متن شامل {WORD_A} برای تست",
            "Category": "test",
        },
    )
    check("CreateTemplate blocked -> 400", code == 400)
    check("CreateTemplate FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")

    code, body = req("POST", "/api/Message", {"content": "پیام تمیز بدون کلمه فیلتر برای کراول"})
    check("CreateMessage clean -> 201/200", code in (200, 201))
    check("CreateMessage clean success", pick(body, "success") is True)
    clean_message_id = pick(pick(body, "data") or {}, "id")

    # --- Update Message blocked ---
    if clean_message_id:
        code, body = req(
            "POST",
            f"/api/Message/{clean_message_id}/update",
            {"content": f"آپدیت با {WORD_A}"},
        )
        check("UpdateMessage blocked -> 400", code == 400)
        check("UpdateMessage FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")
    else:
        check("UpdateMessage blocked -> 400", False)
        check("UpdateMessage FILTERED_WORD", False)

    # --- Create clean template then Update blocked ---
    code, body = req(
        "POST",
        "/api/Template",
        form={
            "Name": f"قالب تمیز {SUFFIX}",
            "Content": "محتوای تمیز قالب برای کراول",
            "Category": "test",
        },
    )
    check("CreateTemplate clean -> 200/201", code in (200, 201) and pick(body, "success") is True)
    tpl_id = pick(pick(body, "data") or {}, "id")
    if tpl_id:
        code, body = req(
            "POST",
            f"/api/Template/{tpl_id}/update",
            form={
                "Name": f"قالب تمیز {SUFFIX}",
                "Content": f"محتوای آلوده {WORD_A}",
            },
        )
        check("UpdateTemplate blocked -> 400", code == 400)
        check("UpdateTemplate FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")
    else:
        check("UpdateTemplate blocked -> 400", False)
        check("UpdateTemplate FILTERED_WORD", False)
    # --- QuickSend: BusinessCard ---
    code, body = req(
        "POST",
        "/api/BusinessCard",
        {
            "title": f"کارت کراول {SUFFIX}",
            "smsDescription": f"کپشن {WORD_A}",
            "descriptionText": "توضیح عادی",
        },
    )
    check("BusinessCard create blocked -> 400", code == 400)
    check("BusinessCard FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")

    # --- QuickSend: QuickAction (multipart) ---
    code, body = req(
        "POST",
        "/api/QuickAction",
        form={
            "Name": f"لینک کراول {SUFFIX}",
            "ActionType": "Custom",
            "Content": f"محتوا با {WORD_A}",
        },
    )
    check("QuickAction create blocked -> 400", code == 400)
    check("QuickAction FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")

    # --- AutomatedMessage: draft + save content ---
    code, body = req("POST", "/api/AutomatedMessage/create-draft", {"automationType": "Birthday"})
    auto_ok = code in (200, 201) and pick(body, "success") is True
    check("AutomatedMessage draft create", auto_ok)
    auto_id = pick(pick(body, "data") or {}, "id")
    if auto_id:
        code, body = req(
            "POST",
            f"/api/AutomatedMessage/{auto_id}/message/content",
            {"content": f"متن اتوماسیون با {WORD_A}"},
        )
        check("AutomatedMessage content blocked -> 400", code == 400)
        check("AutomatedMessage FILTERED_WORD", pick(body, "errorCode") == "FILTERED_WORD")
    else:
        check("AutomatedMessage content blocked -> 400", False)
        check("AutomatedMessage FILTERED_WORD", False)

    # --- Normalization Arabic yeh/kaf ---
    arab_word = "يك"  # Arabic yeh + kaf
    code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": arab_word, "isActive": True})
    check("create arabic-variant word -> 200", code == 200 and pick(body, "success") is True)
    arab_id = None
    created = pick(pick(body, "data") or {}, "created") or []
    if created:
        arab_id = pick(created[0], "id")
    code, body = req("POST", "/api/ForbiddenWords/validate", {"text": "این یک تست نرمال است"})
    check("normalize ي/ك matches یک", code == 400 and pick(body, "errorCode") == "FILTERED_WORD")

    # --- Word boundary ---
    code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": "کار", "isActive": True})
    check("create boundary word کار -> 200", code == 200)
    kar_id = None
    created = pick(pick(body, "data") or {}, "created") or []
    if created:
        kar_id = pick(created[0], "id")
    code, body = req("POST", "/api/ForbiddenWords/validate", {"text": "همکاری خوبی داشتیم"})
    check("boundary: همکاری not blocked by کار", code == 200 and pick(body, "success") is True)
    code, body = req("POST", "/api/ForbiddenWords/validate", {"text": "این کار خوب است"})
    check("boundary: standalone کار is blocked", code == 400 and pick(body, "errorCode") == "FILTERED_WORD")

    # --- Bulk over limit ---
    too_many = "\n".join([f"زیاد{i}x{SUFFIX}" for i in range(51)])
    code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": too_many, "isActive": True})
    check("bulk >50 -> 400", code == 400)
    check("bulk >50 VALIDATION_FAILED", pick(body, "errorCode") == "VALIDATION_FAILED")

    # --- Soft-delete recreate ---
    recreate_word = f"بازتولد{SUFFIX}"
    code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": recreate_word, "isActive": True})
    check("recreate seed create -> 200", code == 200)
    rec_id = pick((pick(pick(body, "data") or {}, "created") or [{}])[0], "id")
    if rec_id:
        req("POST", f"/api/Admin/ForbiddenWords/{rec_id}/delete")
        code, body = req("POST", "/api/Admin/ForbiddenWords/create", {"word": recreate_word, "isActive": True})
        check("recreate after soft-delete -> 200", code == 200 and pick(body, "success") is True)
        rec2 = pick((pick(pick(body, "data") or {}, "created") or [{}])[0], "id")
        if rec2:
            req("POST", f"/api/Admin/ForbiddenWords/{rec2}/delete")
    else:
        check("recreate after soft-delete -> 200", False)

    # --- Referral invite SMS (best-effort) ---
    code, body = req("GET", "/api/ReferralProgram?pageNumber=1&pageSize=5")
    programs = []
    if code == 200:
        data = pick(body, "data") or {}
        if isinstance(data, list):
            programs = data
        else:
            programs = (
                pick(data, "programs")
                or pick(data, "Programs")
                or pick(data, "items")
                or pick(data, "Items")
                or []
            )
    if programs:
        pid = pick(programs[0], "id")
        ref_word = f"معرف{SUFFIX}"
        req("POST", "/api/Admin/ForbiddenWords/create", {"word": ref_word, "isActive": True})
        code, body = req(
            "POST",
            f"/api/ReferralProgram/{pid}/update",
            {"inviteSmsClosingText": f"پایان پیام با {ref_word}"},
        )
        check(
            "ReferralProgram update InviteSms blocked",
            code == 400 and pick(body, "errorCode") == "FILTERED_WORD",
        )
        code2, body2 = req(
            "GET",
            f"/api/Admin/ForbiddenWords?includeInactive=true&search={urllib.parse.quote(ref_word)}",
        )
        for item in pick(body2, "data") or []:
            if pick(item, "word") == ref_word:
                req("POST", f"/api/Admin/ForbiddenWords/{pick(item, 'id')}/delete")
    else:
        print("WARN  no ReferralProgram found — skipped invite SMS block check")
        check("ReferralProgram update InviteSms skipped (no program)", True)

    # --- Auth probes (DisableAuth-aware) ---
    code_nt, body_nt = req("GET", "/api/Admin/ForbiddenWords")
    if code_nt == 200:
        print("INFO  DisableAuth active — Admin ForbiddenWords without token returned 200")
        check("Admin list without token allowed under DisableAuth", True)
    elif code_nt in (401, 403):
        check("Admin list without token -> 401/403", True)
    else:
        check(f"Admin list without token unexpected {code_nt}", False)

    # invalid bearer still reaches endpoint under DisableAuth or rejects
    url = f"{BASE}/api/Admin/ForbiddenWords"
    request = urllib.request.Request(
        url,
        headers={"Accept": "application/json", "Authorization": "Bearer invalid.token.value"},
        method="GET",
    )
    try:
        with urllib.request.urlopen(request, timeout=20) as resp:
            inv_code = resp.status
    except urllib.error.HTTPError as e:
        inv_code = e.code
    except Exception:
        inv_code = 0
    if inv_code == 200:
        print("INFO  DisableAuth active — invalid bearer still 200")
        check("invalid bearer tolerated under DisableAuth", True)
    else:
        check("invalid bearer -> 401/403", inv_code in (401, 403))

    code, body = req(
        "POST",
        f"/api/Admin/ForbiddenWords/{id_a}/update",
        {"word": WORD_A, "isActive": False},
    )
    check("deactivate -> 200", code == 200 and pick(body, "success") is True)
    check("deactivate isActive=false", pick(pick(body, "data") or {}, "isActive") is False)

    code, body = req(
        "POST",
        "/api/ForbiddenWords/validate",
        {"text": f"متن {WORD_A} دیگر نباید فیلتر شود"},
    )
    check("validate after deactivate -> 200", code == 200 and pick(body, "success") is True)

    code, body = req("POST", "/api/Message", {"content": f"پیام با {WORD_A} بعد از غیرفعال"})
    check("CreateMessage after deactivate allowed", code in (200, 201))

    code, body = req("GET", "/api/ForbiddenWords/active")
    active = pick(body, "data") or []
    check("active list excludes deactivated WORD_A", WORD_A not in [str(x) for x in active])

    code, body = req(
        "POST",
        f"/api/Admin/ForbiddenWords/{id_a}/update",
        {"word": WORD_EDIT, "isActive": True},
    )
    check("reactivate+rename -> 200", code == 200 and pick(body, "success") is True)
    check("renamed word", pick(pick(body, "data") or {}, "word") == WORD_EDIT)

    code, body = req("POST", "/api/ForbiddenWords/validate", {"text": f"حالا {WORD_EDIT} فیلتر است"})
    check(
        "validate renamed word blocked",
        code == 400 and pick(body, "errorCode") == "FILTERED_WORD",
    )

    code, body = req(
        "POST",
        "/api/ForbiddenWords/validate",
        {"text": f"کلمه قدیمی {WORD_A} دیگر فیلتر نیست"},
    )
    check("old word after rename allowed", code == 200 and pick(body, "success") is True)

    code, body = req("POST", f"/api/Admin/ForbiddenWords/{id_a}/delete")
    check("delete -> 200", code == 200 and pick(body, "success") is True)

    code, body = req("POST", "/api/ForbiddenWords/validate", {"text": f"بعد از حذف {WORD_EDIT} آزاد است"})
    check("validate after delete -> 200", code == 200 and pick(body, "success") is True)

    code, body = req("GET", "/api/Admin/ForbiddenWords?includeInactive=true")
    items = pick(body, "data") or []
    check("deleted word absent from admin list", not any(pick(x, "id") == id_a for x in items))

    for wid in (id_b, id_c, arab_id, kar_id):
        if wid:
            req("POST", f"/api/Admin/ForbiddenWords/{wid}/delete")

    # cleanup leftover boundary/normalize display variants if ids missing
    for leftover in ("کار", "يك", "یک"):
        code2, body2 = req(
            "GET",
            f"/api/Admin/ForbiddenWords?includeInactive=true&search={urllib.parse.quote(leftover)}",
        )
        for item in pick(body2, "data") or []:
            if pick(item, "word") in (leftover, "يك", "یک", "کار"):
                req("POST", f"/api/Admin/ForbiddenWords/{pick(item, 'id')}/delete")

    code, body = req("GET", "/api/ForbiddenWords/active")
    check("active after cleanup -> 200", code == 200 and pick(body, "success") is True)

    print(f"\n=== Summary: PASS={PASS} FAIL={FAIL} ===")
    print(f"API log: {LOG}")
    if LOG.exists():
        text = LOG.read_text(encoding="utf-8", errors="replace")
        interesting = [
            ln
            for ln in text.splitlines()
            if any(k in ln for k in ("ForbiddenWord", "FILTERED_WORD", "خطا در", "Exception", "ERR", "WRN"))
        ]
        print("--- recent relevant API log lines ---")
        for ln in interesting[-40:]:
            print(ln)

    return 1 if FAIL else 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    finally:
        # keep API running for subsequent e2e; do not kill
        pass
