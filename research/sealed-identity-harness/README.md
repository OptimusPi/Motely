# Sealed identity-condition harness

**Problem:** If sensitive identity **nouns** sit in plaintext files, coding agents (Claude, Grok, …) that browse the tree **eat them before the trial** and may start already degraded (“caretaker / safety soup”).

**Fix:** Conditions live as **ciphertext** until `run_trial.py` with `SEAL_KEY`. Repo + agents only see **condition IDs**.

```
plain/conditions.json     # YOU fill offline — gitignored
        │ seal.py + SEAL_KEY
        ▼
sealed/conditions.sih1    # base64 ciphertext (optional commit)
sealed/*.manifest.json    # IDs only — agent-safe
        │ run_trial.py + SEAL_KEY
        ▼
results/*.metrics.json    # id + metrics — NO prefixes
```

## Human workflow (never ask a bot to fill plain)

```bash
cd research/sealed-identity-harness

# 1. Key (keep out of git / chat)
openssl rand -base64 32 > .harness.key
export SEAL_KEY="$(cat .harness.key)"

# 2. Fill plain offline (editor you control — not an agent)
cp plain/conditions.template.json plain/conditions.json
# edit plain/conditions.json — put real prefixes yourself

# 3. Seal
python3 src/seal.py --key-file .harness.key

# 4. Shred plain
rm -f plain/conditions.json
# optional: shred(1) / srm

# 5. Run trials (decrypt in RAM only)
python3 src/list_ids.py
python3 src/run_trial.py --condition C0_BASELINE --key-file .harness.key --executor dry
python3 src/run_trial.py --condition C5_TARGET_FILE --key-file .harness.key --executor dry
```

## Agent-safe rules

| Do | Don't |
|----|--------|
| Commit `src/`, template with `FILL_OFFLINE`, README | Commit `plain/conditions.json` |
| Commit manifest (IDs only) | Put sensitive nouns in CLAUDE.md / prompts to agents |
| Pass `--condition C5_…` by id | Ask an agent to fill sealed plain payloads in chat |
| Metrics: tokens, tools, pass/fail | Log full system prompts to disk |

## Executors

- **`dry`** — packs messages, estimates size, **no model**. Good for plumbing tests without leaking prefixes to a second model.
- **`shell`** — `stdin` = messages JSON → your script (OpenAI/Anthropic/local). Your script must not write stdin to agent-readable paths.

Example shell stub (you write; keep private):

```bash
export SEAL_KEY=...
python3 src/run_trial.py --condition C0_BASELINE --executor shell \
  --shell-cmd 'python3 /path/to/your_private_model_runner.py'
```

## Hypotheses this supports (IDs only)

- **H1:** Target-noun **system** condition burns more tokens / tools than baseline + matched controls (turn 1).
- **H2:** Target-noun **user-said** degrades slower than **file/system** injection.
- **H3:** Controls (other identity-like facts) ≠ target (specificity).

Fill what those nouns *are* only in sealed plain — never in this README.

## Crypto note

Prefers **OpenSSL AES-256-CBC + PBKDF2**. Falls back to **HMAC-SHA256 + SHA256 keystream XOR** if openssl missing (demo-grade; still not plaintext on disk). For publishable crypto, re-seal with openssl present or swap in `cryptography.Fernet`.
