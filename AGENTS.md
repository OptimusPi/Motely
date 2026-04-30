# AGENTS.md

## Agent Instructions for jaml-ui

1. **Design System Adherence:** Agents MUST strictly adhere to the `DESIGN.md` document. 
   - Never use ALL CAPS.
   - Never use font-weight bold or heavy.
   - Never use grey text on a grey background.
   - Ensure the UI is mobile-first (375px width).
   - Use CSS animations (e.g., `.j-font-dance-char`, `scale(1.05) translateY(-2px)`) for "juice-up" effects, not unnecessary JS wrappers.
   - Never add visible scrollbars. Use magnetic scroll snapping.

2. **Motely Integration:** `motely-wasm` should be imported and booted globally at the module level. Do NOT use redundant JS wrappers around it. Search Workers should use Vite's `?worker&inline` pattern instead of blobs. `motelyWasmUrl` prop-drilling is deprecated and no longer needed.

3. **General Coding Standards:** Check `CLAUDE.md` for specific module boundary and TypeScript conventions. Always respect the existing architecture.
