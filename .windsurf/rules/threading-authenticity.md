---
description: Enforce real WASM threading behavior and fail-fast diagnostics
globs:
  - "Motely.BrowserWasm/**/*.cs"
alwaysApply: true
---
# Threading authenticity

Do not pretend multithreading is active in browser runtime.

- If caller requests `threadCount > 1`, verify runtime capability first.
- If runtime cannot satisfy request, return explicit error with remediation.
- Remediation message must mention:
  - COOP/COEP headers
  - JS loader thread enablement
  - observed runtime processor count

## Implementation guardrails

- Do not add hidden clamping/defaulting for requested thread count.
- Do not spin unsupported background thread APIs in browser-only code paths.
- Keep runtime checks cheap and deterministic.
