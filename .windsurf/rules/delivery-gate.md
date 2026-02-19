---
description: Define done criteria for shipping CLI and WASM safely
globs:
  - "**/*"
alwaysApply: true
---
# Delivery gate

A task is not done until all required build/run gates pass.

## Mandatory gates

1. `dotnet build Motely.BrowserWasm/Motely.BrowserWasm.csproj` passes.
2. `dotnet build Motely.CLI/Motely.CLI.csproj` passes.
3. WASM capability check reports expected runtime flags.
4. CLI smoke command runs and returns expected output shape.

## Reporting format

- Report each gate as PASS/FAIL.
- For FAIL, provide the exact error and the next fix action.
- Do not claim completion while any mandatory gate is failing.
