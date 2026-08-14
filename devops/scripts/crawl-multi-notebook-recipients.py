#!/usr/bin/env python3
"""
Crawl: انتخاب چند دفترچه تلفن در ارسال پیام و پیام خودکار.

Usage:
  python3 devops/scripts/crawl-multi-notebook-recipients.py [BASE_URL]
"""
from __future__ import annotations

import json
import sys
import time
import urllib.error
import urllib.request
from typing import Any

BASE = sys.argv[1] if len(sys.argv) > 1 else "http://127.0.0.1:5054"
PASS = 0
FAIL = 0
STAMP = str(int(time.time()))[-7:]


def log(msg: str) -> None:
    print(msg, flush=True)


def check(name: str, cond: bool, detail: str = "") -> None:
    global PASS, FAIL
    if cond:
        PASS += 1
        log(f"PASS  {name}" + (f" — {detail}" if detail else ""))
    else:
        FAIL += 1
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


def req_form(path: str, fields: dict[str, str], timeout: int = 60) -> tuple[int, dict]:
    boundary = "----VappCrawlBoundary"
    chunks: list[bytes] = []
    for key, value in fields.items():
        chunks.append(f"--{boundary}\r\n".encode())
        chunks.append(f'Content-Disposition: form-data; name="{key}"\r\n\r\n'.encode())
        chunks.append(value.encode("utf-8"))
        chunks.append(b"\r\n")
    chunks.append(f"--{boundary}--\r\n".encode())
    body = b"".join(chunks)
    headers = {
        "Accept": "application/json",
        "Content-Type": f"multipart/form-data; boundary={boundary}",
    }
    r = urllib.request.Request(BASE + path, data=body, headers=headers, method="POST")
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


def create_notebook(name: str) -> int:
    code, d = req_form("/api/ContactNotebook", {"Name": name, "IsActive": "true", "Description": "crawl multi-select"})
    nid = jget(d, "data", "id")
    check(f"create notebook {name}", code in (200, 201) and bool(nid), f"code={code} id={nid} msg={jget(d,'message')}")
    return int(nid) if nid else 0


def create_contact(notebook_id: int, mobile: str, full_name: str) -> int:
    code, d = req(
        "POST",
        "/api/Contact",
        {
            "contactNotebookId": notebook_id,
            "mobileNumber": mobile,
            "fullName": full_name,
        },
    )
    cid = jget(d, "data", "id")
    check(f"create contact {mobile} in notebook {notebook_id}", code in (200, 201) and bool(cid), f"code={code} id={cid}")
    return int(cid) if cid else 0


def create_message() -> int:
    code, d = req("POST", "/api/Message", {"content": f"تست چند دفترچه {STAMP}"})
    mid = jget(d, "data", "id")
    check("create message", code in (200, 201) and bool(mid), f"code={code} id={mid}")
    return int(mid) if mid else 0


def mobiles(resp: dict) -> set[str]:
    items = jget(resp, "data", "recipients") or []
    return {str(i.get("mobileNumber")) for i in items if isinstance(i, dict)}


def main() -> int:
    log(f"=== Multi-notebook recipients crawl @ {BASE} ===")

    nb1 = create_notebook(f"کراول دفترچه الف {STAMP}")
    nb2 = create_notebook(f"کراول دفترچه ب {STAMP}")
    if not nb1 or not nb2:
        log("ABORT: could not create notebooks")
        return 1

    mobile_a = f"0912{(STAMP + '0000000')[:7]}"
    mobile_b = f"0913{(STAMP + '0000000')[:7]}"
    mobile_dup = f"0914{(STAMP + '0000000')[:7]}"

    c1 = create_contact(nb1, mobile_a, "مخاطب الف")
    c2 = create_contact(nb2, mobile_b, "مخاطب ب")
    c3a = create_contact(nb1, mobile_dup, "مخاطب مشترک الف")
    c3b = create_contact(nb2, mobile_dup, "مخاطب مشترک ب")
    if not all([c1, c2, c3a, c3b]):
        log("ABORT: could not create contacts")
        return 1

    message_id = create_message()
    if not message_id:
        return 1

    # 1) Happy path — two notebooks
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {"messageId": message_id, "selectionType": "Notebook", "contactNotebookIds": [nb1, nb2]},
    )
    got = mobiles(d)
    check(
        "Message multi-notebook success",
        code == 200 and jget(d, "success") is True and jget(d, "data", "totalCount") == 3,
        f"code={code} count={jget(d,'data','totalCount')} mobiles={sorted(got)} err={jget(d,'errorCode')}",
    )
    check(
        "Message multi-notebook dedupes shared mobile",
        mobile_dup in got and mobile_a in got and mobile_b in got and len(got) == 3,
        f"got={sorted(got)}",
    )
    check("Message multi-notebook has sessionId", bool(jget(d, "data", "sessionId")), f"sessionId={jget(d,'data','sessionId')}")

    # 2) Backward compat — singular contactNotebookId
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {"messageId": message_id, "selectionType": "Notebook", "contactNotebookId": nb1},
    )
    got = mobiles(d)
    check(
        "Message singular contactNotebookId still works",
        code == 200 and jget(d, "success") is True and mobile_a in got and mobile_b not in got,
        f"code={code} count={jget(d,'data','totalCount')} mobiles={sorted(got)}",
    )

    # 3) Merge singular + list
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {
            "messageId": message_id,
            "selectionType": "Notebook",
            "contactNotebookIds": [nb1],
            "contactNotebookId": nb2,
        },
    )
    got = mobiles(d)
    check(
        "Message merges ContactNotebookIds + ContactNotebookId",
        code == 200 and mobile_a in got and mobile_b in got,
        f"code={code} count={jget(d,'data','totalCount')}",
    )

    # 4) Duplicate ids in list
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {"messageId": message_id, "selectionType": "Notebook", "contactNotebookIds": [nb1, nb1, nb2]},
    )
    check(
        "Message duplicate notebook ids are distinct",
        code == 200 and jget(d, "data", "totalCount") == 3,
        f"code={code} count={jget(d,'data','totalCount')}",
    )

    # 5) Exclude a contact from one of the notebooks
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {
            "messageId": message_id,
            "selectionType": "Notebook",
            "contactNotebookIds": [nb1, nb2],
            "contactIds": [c1],
        },
    )
    got = mobiles(d)
    check(
        "Message exclude contact across notebooks",
        code == 200 and mobile_a not in got and mobile_b in got,
        f"code={code} mobiles={sorted(got)}",
    )

    # 6) Empty list
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {"messageId": message_id, "selectionType": "Notebook", "contactNotebookIds": []},
    )
    check(
        "Message empty notebook list -> VALIDATION_FAILED",
        jget(d, "success") is False and jget(d, "errorCode") == "VALIDATION_FAILED" and code == 400,
        f"code={code} errorCode={jget(d,'errorCode')} msg={jget(d,'message')}",
    )

    # 7) Missing messageId
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {"selectionType": "Notebook", "contactNotebookIds": [nb1]},
    )
    check(
        "Message missing messageId -> VALIDATION_FAILED",
        code == 400 and jget(d, "errorCode") == "VALIDATION_FAILED",
        f"code={code} errorCode={jget(d,'errorCode')} errors={jget(d,'errors')}",
    )

    # 8) Invalid notebook id
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {"messageId": message_id, "selectionType": "Notebook", "contactNotebookIds": [nb1, 999999]},
    )
    check(
        "Message invalid notebook -> INVALID_INPUT",
        code == 400 and jget(d, "errorCode") == "INVALID_INPUT",
        f"code={code} errorCode={jget(d,'errorCode')} msg={jget(d,'message')}",
    )

    # 9) Message not found
    code, d = req(
        "POST",
        "/api/Message/recipients/select",
        {"messageId": 999999, "selectionType": "Notebook", "contactNotebookIds": [nb1]},
    )
    check(
        "Message missing message -> 400",
        code == 400 and jget(d, "success") is False,
        f"code={code} msg={jget(d,'message')} errorCode={jget(d,'errorCode')}",
    )

    # 10) Invalid token
    url = BASE + "/api/Message/recipients/select"
    body = json.dumps({"messageId": message_id, "selectionType": "Notebook", "contactNotebookIds": [nb1]}).encode()
    r = urllib.request.Request(
        url,
        data=body,
        headers={
            "Accept": "application/json",
            "Content-Type": "application/json",
            "Authorization": "Bearer not-a-real-token",
        },
        method="POST",
    )
    try:
        with urllib.request.urlopen(r, timeout=30) as resp:
            token_code = resp.status
            token_d = json.loads(resp.read().decode("utf-8") or "{}")
    except urllib.error.HTTPError as e:
        token_code = e.code
        try:
            token_d = json.loads(e.read().decode("utf-8") or "{}")
        except Exception:
            token_d = {}
    check(
        "Message invalid bearer -> 401 TOKEN_INVALID",
        token_code == 401 and jget(token_d, "errorCode") in ("TOKEN_INVALID", "UNAUTHORIZED"),
        f"code={token_code} errorCode={jget(token_d,'errorCode')} msg={jget(token_d,'message')}",
    )

    # --- Automated message ---
    code, d = req("POST", "/api/AutomatedMessage/create-draft", {"automationType": "Birthday"})
    am_id = jget(d, "data", "id")
    check("Automated create-draft", code in (200, 201) and bool(am_id), f"id={am_id}")
    am_id = int(am_id) if am_id else 0

    if am_id:
        code, d = req(
            "POST",
            f"/api/AutomatedMessage/{am_id}/recipients/select",
            {"applyToAllContacts": False, "contactNotebookIds": [nb1, nb2]},
        )
        check(
            "Automated multi-notebook success",
            code == 200 and jget(d, "success") is True and jget(d, "data", "totalCount") >= 3,
            f"code={code} count={jget(d,'data','totalCount')} err={jget(d,'errorCode')} msg={jget(d,'message')}",
        )

        code, d = req(
            "POST",
            f"/api/AutomatedMessage/{am_id}/recipients/select",
            {"applyToAllContacts": False, "contactNotebookId": nb1},
        )
        check(
            "Automated singular contactNotebookId still works",
            code == 200 and jget(d, "success") is True,
            f"code={code} count={jget(d,'data','totalCount')}",
        )

        code, d = req(
            "POST",
            f"/api/AutomatedMessage/{am_id}/recipients/select",
            {"applyToAllContacts": False},
        )
        check(
            "Automated no notebook -> INVALID_INPUT",
            code == 400 and jget(d, "errorCode") == "INVALID_INPUT",
            f"code={code} errorCode={jget(d,'errorCode')} msg={jget(d,'message')}",
        )

        code, d = req(
            "POST",
            f"/api/AutomatedMessage/{am_id}/recipients/select",
            {"applyToAllContacts": False, "contactNotebookIds": [999999]},
        )
        check(
            "Automated invalid notebook -> INVALID_INPUT",
            code == 400 and jget(d, "errorCode") == "INVALID_INPUT",
            f"code={code} errorCode={jget(d,'errorCode')} msg={jget(d,'message')}",
        )

        code, d = req(
            "POST",
            "/api/AutomatedMessage/999999/recipients/select",
            {"applyToAllContacts": False, "contactNotebookIds": [nb1]},
        )
        check(
            "Automated missing id -> NOT_FOUND",
            code == 404 and jget(d, "errorCode") == "NOT_FOUND",
            f"code={code} errorCode={jget(d,'errorCode')} msg={jget(d,'message')}",
        )

        code, d = req(
            "POST",
            f"/api/AutomatedMessage/{am_id}/recipients/select",
            {"applyToAllContacts": True},
        )
        check(
            "Automated applyToAllContacts still works",
            code == 200 and jget(d, "success") is True and jget(d, "data", "totalCount") >= 3,
            f"code={code} count={jget(d,'data','totalCount')}",
        )

    log("")
    log(f"=== RESULT: PASS={PASS} FAIL={FAIL} ===")
    return 1 if FAIL else 0


if __name__ == "__main__":
    sys.exit(main())
