# Next.js + Turbopack + `motely-wasm` / `motely-node` — facts (for humans and agents)

## 1. `pnpm onlyBuiltDependencies` is not a Next.js fix

- **What it is:** pnpm’s allowlist for which packages may run **lifecycle scripts** (`postinstall`, etc.). See pnpm docs for `onlyBuiltDependencies` / `approve-builds`.
- **What it is not:** It does **not** tell Turbopack or webpack how to resolve `fs`, `fs/promises`, or Bootsharp’s Node entry.
- **Implication:** Putting `motely-wasm` there does **not** fix `Module not found: fs/promises` in the **browser** bundle.

## 2. `serverExternalPackages` is server-only

- **Purpose:** Opt out of bundling certain **Node** dependencies in **server** contexts so they load at runtime (native addons, odd CJS, etc.).
- **`motely-node`:** Belongs here on Vercel/server routes that `import` it — typical and correct.
- **`motely-wasm`:** Browser/Bootsharp path. Listing it in `serverExternalPackages` does **not** fix client SSR graph issues; the failure mode is usually **client** / **SSR of a client component** pulling `motely-wasm`’s **Node** shims.

Refs: [Next.js: `serverExternalPackages`](https://nextjs.org/docs/app/api-reference/config/next-config-js/serverExternalPackages), [Turbopack + externals issues](https://github.com/vercel/next.js/issues/65828).

## 3. `fs/promises` in the browser bundle

- **Cause:** A **static** import chain from a Client Component (or SSR of it) into `motely-wasm` reaches Bootsharp’s **Node**-oriented code, which references Node built-ins.
- **Fixes (pick one or combine):**
  - **Graph:** Do not import `motely-wasm` on the server path. Load only inside `useEffect` / dynamic `import()` after mount, from client-only modules.
  - **Bundler:** For Turbopack, use **`turbopack.resolveAlias`** with a **`browser`** condition pointing `fs` / `fs/promises` at an empty stub module (documented pattern). See [next.config.js: turbopack](https://nextjs.org/docs/app/api-reference/config/next-config-js/turbopack) and [Turbopack API reference](https://nextjs.org/docs/app/api-reference/turbopack).

## 4. “Failed to load the ES module” on Vercel + `motely-node`

- Often the **truncated Node warning** is about loading a **native `.node` addon** or its **dependencies** (glibc, `dlopen`, wrong arch, missing file in the function bundle) — **not** proof that “ESM vs CJS” is the root cause.
- **Debug:** Full stderr, verify `Motely.NodeAddon.node` is in the deployed artifact, architecture matches (e.g. linux-x64 on Vercel), and `serverExternalPackages` / output tracing includes the binary.

## 5. Bootsharp API vs “glue”

- **Generated API:** `motely-wasm` exposes Bootsharp-generated bindings (e.g. `MotelyWasm.MotelyWasmBackend.*`). That is the contract.
- **JSON strings:** Methods that return `Promise<string>` JSON need **one** parsing boundary (Zod/schema or typed parse) — not `unknown` everywhere, not duplicated parse logic in every component.
- **Boot singleton:** A tiny module (e.g. `ensureWasmBooted()`) is appropriate; a second full “facade” that re-exports everything is optional — if it exists, it must **match** backend capabilities (e.g. thread count) and **not** drop fields consumers already use.

## 6. Migration hygiene

- **Never** leave hooks or routes at **0 bytes** during refactors; restore from git immediately.
- **Avoid** clever one-liners like `return (p ??= boot().then(...))` — linters (e.g. Sonar assignment-in-expression rules) and future you will hate it. Use explicit `if (!p) p = …; return p;`.

## 7. JAML schema in this repo

- **Do not** hand-edit mirrored `jaml.schema.json` files. Regenerate with `Motely.CLI --write-jaml-schema`. See `AGENTS.md` in this repository.
