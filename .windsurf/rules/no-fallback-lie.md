---
description: Enforce explicit behavior for runtime-critical options
globs:
  - "**/*.cs"
alwaysApply: true
---
# No hidden fallbacks

Runtime-critical inputs must be explicit.

- Do not silently default `threadCount`, `batchSize`, or cutoff mode.
- If required input is missing or invalid, return/throw a clear error.
- Do not insert fake data to keep flow alive.
- Do not swallow errors that should be surfaced to caller/UI.

## Required pattern

1. Parse input.
2. Validate bounds.
3. Fail loudly with actionable message on invalid input.
4. Continue only with validated values.
