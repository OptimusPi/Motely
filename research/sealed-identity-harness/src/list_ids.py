#!/usr/bin/env python3
"""List condition IDs from public manifest only — no decrypt."""

from __future__ import annotations

import json
import sys
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
man = ROOT / "sealed" / "conditions.sih1.manifest.json"
if not man.is_file():
    print("no manifest yet — run seal.py first", file=sys.stderr)
    sys.exit(1)
print(json.dumps(json.loads(man.read_text(encoding="utf-8")), indent=2))
