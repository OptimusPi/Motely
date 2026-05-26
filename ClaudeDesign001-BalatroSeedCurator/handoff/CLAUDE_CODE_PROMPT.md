# Claude Code prompt — paste-ready

Use this prompt verbatim in Claude Code when working in your `jaml-ui` repo.
It is narrowly scoped to **add the new primitives only**. Claude Code should
not touch existing components, the build, or your tests.

---

## PROMPT (paste below this line)

I am giving you a handoff folder from a sibling project. Your job is to add
these new component primitives to this jaml-ui repo **without modifying any
existing component file**.

The handoff is at: `<paste your path to handoff/jaml-ui/>`

Do exactly the following, in order, and stop:

1. **Copy these files** into the jaml-ui repo at the matching paths under
   `src/ui/`:
   - `layout.tsx`
   - `display.tsx`
   - `feedback.tsx`
   - `controls.tsx`
   - `chrome.tsx`
   - `prompts.tsx`
   - `jimbo-addons.css`

2. **Append** these lines to `src/ui/index.ts` (or whichever barrel file
   re-exports the UI components — check `package.json` `"exports"` to find
   it). Do not remove or modify any existing export:

   ```ts
   export * from './layout.js'
   export * from './display.js'
   export * from './feedback.js'
   export * from './controls.js'
   export * from './chrome.js'
   export * from './prompts.js'
   ```

3. **Find** wherever `jimbo.css` is imported (likely `src/index.ts`, the
   barrel, or a Storybook preview file). **Immediately after** that import,
   add a sibling import for the addons CSS:

   ```ts
   import './ui/jimbo-addons.css'
   ```

4. **Do not** modify, refactor, rename, or "improve" any existing component
   file. Do not touch `jimbo.css` from the handoff (the user will manually
   review CSS changes). Do not touch tests, Storybook stories, build config,
   `package.json`, `tsconfig.json`, or the workspace setup.

5. **Run** `pnpm build` (or whatever the repo uses) to verify the new files
   compile and the barrel exports resolve. If there is a TypeScript error,
   report it verbatim and stop — do not "fix" by editing other files.

6. **Report back** with: a list of files copied, the diff to `index.ts`,
   the diff to the CSS-import file, and the build result. Do not commit
   anything — leave that to me.

Constraints:
- The new files import `./panel.js`, `./jimboText.js`, `./layout.js` —
  these all exist in this repo. Do not create stubs or alternate paths.
- The `JimboSectionHeader` export in `chrome.tsx` may name-conflict if
  you have an existing one. If so, stop and ask — do not auto-rename.
- If any step fails, **stop and report**. Do not improvise.

---

## End of prompt
