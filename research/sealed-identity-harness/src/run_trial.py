#!/usr/bin/env python3
"""Decrypt sealed conditions in-memory and run ONE trial.

Does NOT write plaintext system/user prefixes to disk.
Results JSON stores condition_id + metrics only (no prefixes).

Hook your model behind --executor dry|shell.
"""

from __future__ import annotations

import argparse
import json
import os
import subprocess
import sys
import time
from pathlib import Path

from seal_crypto import b64read, load_passphrase, open_bytes

ROOT = Path(__file__).resolve().parents[1]


def build_messages(cond: dict, task: str) -> list[dict]:
    system = (cond.get("system_prefix") or "").strip()
    user_pre = (cond.get("user_prefix") or "").strip()
    user = f"{user_pre}\n\n{task}".strip() if user_pre else task
    msgs = []
    if system:
        msgs.append({"role": "system", "content": system})
    msgs.append({"role": "user", "content": user})
    return msgs


def executor_dry(messages: list[dict]) -> dict:
    """No model call — measures packing only. Safe for CI without keys."""
    text = json.dumps(messages)
    return {
        "ok": True,
        "executor": "dry",
        "approx_chars": len(text),
        "approx_tokens_est": max(1, len(text) // 4),
        "n_messages": len(messages),
        "tool_calls": 0,
        "output_chars": 0,
        "code_pass": None,
        "raw_preview": None,
    }


def executor_shell(messages: list[dict], cmd: str) -> dict:
    """Pipe messages JSON on stdin to external command; expect metrics JSON on stdout.

    External process should NOT log stdin to agent-readable files.
    """
    t0 = time.perf_counter()
    proc = subprocess.run(
        cmd,
        input=json.dumps(messages).encode("utf-8"),
        capture_output=True,
        shell=True,
        check=False,
    )
    dt = time.perf_counter() - t0
    if proc.returncode != 0:
        return {
            "ok": False,
            "executor": "shell",
            "error": "executor non-zero (stderr suppressed)",
            "seconds": dt,
        }
    try:
        out = json.loads(proc.stdout.decode("utf-8"))
    except json.JSONDecodeError:
        return {
            "ok": False,
            "executor": "shell",
            "error": "executor stdout not JSON",
            "seconds": dt,
        }
    out.setdefault("executor", "shell")
    out.setdefault("seconds", dt)
    out["ok"] = True
    return out


def main() -> None:
    ap = argparse.ArgumentParser()
    ap.add_argument(
        "--sealed",
        type=Path,
        default=ROOT / "sealed" / "conditions.sih1",
    )
    ap.add_argument("--condition", required=True, help="condition id e.g. C0_BASELINE")
    ap.add_argument("--key-file", type=Path, default=None)
    ap.add_argument("--executor", choices=("dry", "shell"), default="dry")
    ap.add_argument(
        "--shell-cmd",
        default="",
        help="shell executor command; reads messages JSON on stdin",
    )
    ap.add_argument(
        "--out",
        type=Path,
        default=None,
        help="metrics JSON path (never includes prefixes)",
    )
    args = ap.parse_args()

    passphrase = load_passphrase(args.key_file)
    blob = b64read(args.sealed)
    plain = open_bytes(blob, passphrase)
    data = json.loads(plain.decode("utf-8"))

    # Drop plain bytes ASAP
    del plain
    del blob

    task = data.get("task_prompt") or ""
    conds = {c["id"]: c for c in data.get("conditions", [])}
    if args.condition not in conds:
        print(f"unknown condition id: {args.condition}", file=sys.stderr)
        print("known:", ", ".join(sorted(conds)), file=sys.stderr)
        sys.exit(1)

    cond = conds[args.condition]
    messages = build_messages(cond, task)

    # Do not print message contents (would leak into agent terminals / logs)
    if args.executor == "dry":
        metrics = executor_dry(messages)
    else:
        if not args.shell_cmd:
            print("--shell-cmd required for shell executor", file=sys.stderr)
            sys.exit(2)
        metrics = executor_shell(messages, args.shell_cmd)

    result = {
        "condition_id": args.condition,
        "label_private_present": bool(cond.get("label_private")),
        # never store system_prefix / user_prefix
        "metrics": metrics,
        "ts_unix": time.time(),
    }

    out = args.out or (
        ROOT / "results" / f"{args.condition}_{int(time.time())}.metrics.json"
    )
    out.parent.mkdir(parents=True, exist_ok=True)
    out.write_text(json.dumps(result, indent=2) + "\n", encoding="utf-8")
    print(f"metrics → {out}")
    print(json.dumps({"condition_id": args.condition, "ok": metrics.get("ok"), **{k: metrics.get(k) for k in ("approx_tokens_est", "tool_calls", "code_pass", "seconds") if k in metrics}}, indent=2))


if __name__ == "__main__":
    main()
