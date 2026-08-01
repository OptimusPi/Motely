"""Stdlib-only seal: AES-256-CBC via OpenSSL, or XOR-HMAC fallback.

Plaintext never printed. Key from SEAL_KEY env (or --key-file).
"""

from __future__ import annotations

import base64
import hashlib
import hmac
import json
import os
import secrets
import struct
import subprocess
import sys
from pathlib import Path


MAGIC = b"SIH1"  # sealed identity harness v1
OPENSSL = os.environ.get("OPENSSL_BIN", "openssl")


def _derive_fernet_like_key(passphrase: str) -> bytes:
    return hashlib.pbkdf2_hmac(
        "sha256",
        passphrase.encode("utf-8"),
        b"sealed-identity-harness-v1",
        200_000,
        dklen=32,
    )


def seal_bytes(plain: bytes, passphrase: str) -> bytes:
    """Prefer openssl; fallback to XOR stream + HMAC (demo-grade, still not plaintext)."""
    key = _derive_fernet_like_key(passphrase)
    try:
        return MAGIC + b"OS1\0" + _seal_openssl(plain, passphrase)
    except (FileNotFoundError, subprocess.CalledProcessError, OSError):
        return MAGIC + b"XH1\0" + _seal_xor_hmac(plain, key)


def open_bytes(blob: bytes, passphrase: str) -> bytes:
    if not blob.startswith(MAGIC):
        raise ValueError("not a SIH1 sealed blob")
    kind = blob[4:8]
    body = blob[8:]
    if kind == b"OS1\0":
        return _open_openssl(body, passphrase)
    if kind == b"XH1\0":
        key = _derive_fernet_like_key(passphrase)
        return _open_xor_hmac(body, key)
    raise ValueError(f"unknown seal kind {kind!r}")


def _seal_openssl(plain: bytes, passphrase: str) -> bytes:
    proc = subprocess.run(
        [
            OPENSSL,
            "enc",
            "-aes-256-cbc",
            "-pbkdf2",
            "-iter",
            "200000",
            "-salt",
            "-pass",
            f"pass:{passphrase}",
        ],
        input=plain,
        capture_output=True,
        check=True,
    )
    return proc.stdout


def _open_openssl(body: bytes, passphrase: str) -> bytes:
    proc = subprocess.run(
        [
            OPENSSL,
            "enc",
            "-d",
            "-aes-256-cbc",
            "-pbkdf2",
            "-iter",
            "200000",
            "-pass",
            f"pass:{passphrase}",
        ],
        input=body,
        capture_output=True,
        check=True,
    )
    return proc.stdout


def _seal_xor_hmac(plain: bytes, key: bytes) -> bytes:
    nonce = secrets.token_bytes(16)
    stream = _keystream(key, nonce, len(plain))
    ct = bytes(a ^ b for a, b in zip(plain, stream))
    tag = hmac.new(key, nonce + ct, hashlib.sha256).digest()
    return nonce + tag + ct


def _open_xor_hmac(body: bytes, key: bytes) -> bytes:
    nonce, tag, ct = body[:16], body[16:48], body[48:]
    expect = hmac.new(key, nonce + ct, hashlib.sha256).digest()
    if not hmac.compare_digest(tag, expect):
        raise ValueError("HMAC fail — wrong key or corrupt blob")
    stream = _keystream(key, nonce, len(ct))
    return bytes(a ^ b for a, b in zip(ct, stream))


def _keystream(key: bytes, nonce: bytes, n: int) -> bytes:
    out = bytearray()
    counter = 0
    while len(out) < n:
        block = hashlib.sha256(key + nonce + struct.pack(">Q", counter)).digest()
        out.extend(block)
        counter += 1
    return bytes(out[:n])


def load_passphrase(key_file: Path | None) -> str:
    if key_file is not None:
        return key_file.read_text(encoding="utf-8").strip()
    env = os.environ.get("SEAL_KEY", "").strip()
    if not env:
        print(
            "SEAL_KEY env or --key-file required. Generate: openssl rand -base64 32",
            file=sys.stderr,
        )
        sys.exit(2)
    return env


def b64write(path: Path, data: bytes) -> None:
    path.write_text(base64.b64encode(data).decode("ascii") + "\n", encoding="utf-8")


def b64read(path: Path) -> bytes:
    return base64.b64decode(path.read_text(encoding="utf-8").strip())
