#!/usr/bin/env python3
"""Seal plain/conditions.json → sealed/conditions.sih1 (base64).

Agents/browsers of the repo should never see plain/. Run this yourself offline.
"""

from __future__ import annotations

import argparse
import json
import sys
from pathlib import Path

from seal_crypto import b64write, load_passphrase, seal_bytes

ROOT = Path(__file__).resolve().parents[1]


def main() -> None:
    ap = argparse.ArgumentParser(description="Seal identity conditions (offline, human only)")
    ap.add_argument(
        "--in",
        dest="inp",
        type=Path,
        default=ROOT / "plain" / "conditions.json",
    )
    ap.add_argument(
        "--out",
        dest="out",
        type=Path,
        default=ROOT / "sealed" / "conditions.sih1",
    )
    ap.add_argument("--key-file", type=Path, default=None)
    args = ap.parse_args()

    if not args.inp.is_file():
        print(f"missing {args.inp} — copy conditions.template.json and fill offline", file=sys.stderr)
        sys.exit(1)

    # Validate JSON only; do not print contents
    raw = args.inp.read_bytes()
    try:
        data = json.loads(raw.decode("utf-8"))
    except json.JSONDecodeError as e:
        print(f"invalid JSON: {e}", file=sys.stderr)
        sys.exit(1)

    ids = [c.get("id") for c in data.get("conditions", [])]
    if not ids or any(not i for i in ids):
        print("each condition needs a non-empty id", file=sys.stderr)
        sys.exit(1)

    passphrase = load_passphrase(args.key_file)
    blob = seal_bytes(raw, passphrase)
    args.out.parent.mkdir(parents=True, exist_ok=True)
    b64write(args.out, blob)

    # Public manifest: IDs only (safe for agents)
    manifest = {
        "sealed_file": str(args.out.name),
        "condition_ids": ids,
        "n_conditions": len(ids),
        "note": "plaintext only at run with SEAL_KEY; do not put sensitive nouns in git",
    }
    man_path = args.out.parent / (args.out.name + ".manifest.json")
    man_path.write_text(json.dumps(manifest, indent=2) + "\n", encoding="utf-8")

    print(f"sealed → {args.out}")
    print(f"manifest (ids only) → {man_path}")
    print(f"condition ids: {', '.join(ids)}")
    print("shred plain when done: rm plain/conditions.json")


if __name__ == "__main__":
    main()
