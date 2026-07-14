# jaml-ui

Package manager is **pnpm** (see `pnpm-lock.yaml`, `pnpm-workspace.yaml`). Never run
`npm install <pkg>` to add/remove a dependency — it generates a stray `package-lock.json`
that conflicts with the committed pnpm lockfile. Use:

- `pnpm add -D <pkg>` (add `-w` if it errors about adding to the workspace root)
- `pnpm remove <pkg>`
- `npm run build` / `npm publish` are fine — those just run existing package.json scripts.

Build entries (`src/index.ts`, `src/ui.ts`, `src/motely.ts`) declare `"use client"`, but
Vite's library build strips module-level directives when bundling — verify `dist/*.js`
still starts with `"use client";` after any vite.config.ts change (see the `banner`
option in `rollupOptions.output`), or Next.js's RSC compiler will silently treat these as
Server Components and crash on any hook use.
