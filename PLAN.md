# PLAN — ship the Balatro seed finder as an MCP App

## What this is

An **MCP App**: a sandboxed HTML/JS UI delivered by an MCP server, rendered inside Claude (and other MCP hosts like Claude Desktop, VS Code Copilot, Goose) when a user invokes a tool. Spec: https://modelcontextprotocol.io/extensions/apps/overview · Build guide: https://modelcontextprotocol.io/extensions/apps/build

Not a generic MCP server. Not a separate web app. Both of those are dead ends for this product — the user wants the seed finder to render inside the Claude conversation.

## Hard constraint: jaml-ui only

All UI uses `jaml-ui` (game components) and `jaml-ui/ui` (the Jimbo design system) — and nothing else. No Tailwind, no shadcn, no MUI, no inline-sketched components in the consumer. If a primitive is missing, add it to `jaml-ui` with a Storybook story and import it from the barrel (per `jaml-ui/CLAUDE.md` "Component placement convention"). The MCP App, the plain SPA, and any future surfaces all consume the same library.

`jimbo.css` is the only stylesheet. It comes in automatically via the side-effect import in `jaml-ui` / `jaml-ui/ui`. Do not author CSS in the consumer.

## The three deliverables

Numbered by priority. Land #1 before touching #2.

1. **MCP App: Balatro Seed Finder.** Tool `find_balatro_seeds` returns a `ui://...` resource. The UI is a small SPA that calls `useSearch`/`useAnalyzer` from `jaml-ui`, runs motely-wasm in the iframe, and renders results using `StandardCard`/`CardFan`/etc. Hosted publicly so any Claude user can connect via custom connector.
2. **Plain web SPA at seedfinder.app.** Same UI components, no MCP wrapping, accessible at a URL. Falls out cheaply once #1 works — same `jaml-ui` consumer, different shell.
3. **Host the Avalonia browser build** (BalatroSeedOracle from `X:\BalatroSeedOracle`) on the same domain at a subpath like `/oracle/`. Static files only. Lowest priority, do not let it block #1.

## Step 1 — install and use the MCP Apps skill

The official skill scaffolds an MCP App correctly. Use it; do not hand-roll the structure.

```
/plugin marketplace add modelcontextprotocol/ext-apps
/plugin install mcp-apps@modelcontextprotocol-ext-apps
```

Restart Claude Code so the skill loads. Verify with: ask "what skills do you have access to?" — `create-mcp-app` should be in the list.

Then:

```
Create an MCP App that finds Balatro seeds matching a JAML filter. Use jaml-ui from the local jaml-ui package for components and motely-wasm for the engine.
```

Build location: `D:\seedfinder.app`. It is currently empty (only `.git/` and `.gitignore`).

## Step 2 — what the MCP App must do

The skill will scaffold the boilerplate (server.ts, mcp-app.html, src/mcp-app.ts, package.json with vite-plugin-singlefile, Express + StreamableHTTPServerTransport on port 3001). Edit it to do these things:

### Server side (`server.ts`)

- Register one tool, `find_balatro_seeds`, with input schema `{ jaml_filter: string, max_results?: number }` and `_meta.ui.resourceUri = "ui://find-balatro-seeds/app.html"`.
- Register the resource at that URI, serving the bundled `dist/mcp-app.html` produced by `vite build`.
- Tool handler can return a stub text result; the real work happens in the UI iframe. (The UI gets the tool input via `app.ontoolresult` and runs the search client-side.)
- CORS open for local dev.

### UI side (`mcp-app.html` + `src/mcp-app.ts`)

- Import `jaml-ui` components and `motely-wasm`. Both must bundle into the single HTML via `vite-plugin-singlefile`. Externalized peers in `jaml-ui`'s `vite.config.ts` (`react`, `react-dom`, `motely-wasm`, etc.) need to be bundled here, not externalized — the iframe loads exactly one file.
- Top-level `await bootsharp.boot({ root: ... })` before mounting React. **Do not use `MotelyProvider` or `useMotelyRuntime`** from `jaml-ui` — `HANDOFF.md` flags them as wrappers that contradict `CLAUDE.md`.
- Mount React. Use the JAML filter from `app.ontoolresult` as initial input, render a `<JamlEditor>` (or textarea), run button, results list using `StandardCard`/`CardFan`.
- Use `app.callServerTool` when the user re-runs with edited input — round-trips through the host so the conversation sees fresh tool calls.

### motely-wasm bin location

motely-wasm needs `/bin/*.wasm` etc. reachable. Two ways to handle inside `vite-plugin-singlefile`:

- **a.** Inline the WASM as a base64 data URL inside the bundled HTML. Increases HTML size by however large the WASM is, but truly single-file.
- **b.** Configure `_meta.ui.csp` on the resource to allow fetches from the MCP server origin, and have the server also serve `node_modules/motely-wasm/bin/*` as static files. Smaller HTML, more moving parts.

Start with (a). If HTML exceeds 5MB and the host refuses to render it, switch to (b).

## Step 3 — hosting

Local dev: `npm run serve` on port 3001 + `npx cloudflared tunnel --url http://localhost:3001`. Use the generated `https://*.trycloudflare.com` URL as a Claude custom connector (Settings → Connectors → Add custom connector). Custom connectors require a paid Claude plan.

Production: deploy the Express server somewhere that runs Node 18+. Options:

- **Fly.io / Railway** — Node + Express, no rewrites needed.
- **Vercel serverless function** — wrap the Express handler in `api/mcp.ts`. Works, but cold starts on each session.
- **Cloudflare Workers** — would require swapping Express for the `WebStandardStreamableHTTPServerTransport` + Hono pattern from the MCP TS SDK docs. Not what the skill scaffolds. Defer unless cost matters.

Pick Fly.io or Railway. Both have free tiers sufficient for a hobby project. Point `seedfinder.app` DNS at it.

## Step 4 — the plain web SPA

Once the MCP App works:

- Extract the React app from `src/mcp-app.ts` into a normal Vite SPA at `D:\seedfinder.app\web/` (or restructure — your call).
- Same `bootsharp.boot()` + same components, no `App` class from `@modelcontextprotocol/ext-apps`.
- Build outputs to `dist/` at the static-hosting root.
- Serve from the same domain at `/` while the MCP endpoint stays at `/mcp`. Both can live in one deploy if the Express server `app.use(express.static('web/dist'))` for the SPA and keeps `/mcp` for MCP.

## Step 5 — host the Avalonia build

`X:\BalatroSeedOracle` is the Avalonia/Browser app. Build it (`dotnet publish` with the Browser/WASM target, output is static files in `bin/Release/.../wwwroot/` or similar — check that project's README).

Drop the static output under `seedfinder.app/oracle/` (or `seedfinder-app/public/oracle/`). One more `app.use('/oracle', express.static(...))` line. No interaction with the MCP server or the SPA — it's an independent SPA mounted at a subpath.

## Open decisions

These are real choices, not "ask the user" — make them based on what works:

- **motely-wasm asset strategy** (inline vs same-origin fetch). Try inline first.
- **Server-side vs iframe-side search.** This plan puts the search in the iframe (the UI calls motely-wasm directly). Alternative: tool handler does the search server-side and returns results to the UI via `ontoolresult`. Server-side requires motely-wasm to run under Node — see `D:\bootsharp\samples\` for whether Node hosting works. Iframe-side avoids that question.
- **Custom connector domain.** Production deploy needs a public HTTPS URL. `seedfinder.app` if DNS is ready, otherwise the Fly/Railway-assigned subdomain.

## Out of scope for v0

- The HANDOFF.md library cleanups (`MotelyProvider` removal, CardFan bow fix, ESLint ignore). Library is good enough to consume.
- Auth on the MCP server. The custom connector dance handles that; no per-user accounts on the seed finder side.
- Multiple tools. One tool, `find_balatro_seeds`, with the UI doing everything else. Add tools only when there's a reason.
- The plain SPA (step 4) and Avalonia hosting (step 5). Land step 1–3 first.

## Done criteria for v0

- Public HTTPS URL where the MCP endpoint responds at `/mcp`.
- Adding it as a Claude custom connector exposes one tool, `find_balatro_seeds`.
- Calling that tool renders an iframe inside the Claude conversation that shows a JAML editor, a run button, and a results list, with motely-wasm actually running searches.
- Results match what `jaml-ui`'s `useSearch` returns for the same JAML — verifiable by running the same filter in Storybook side-by-side.

## Stretch / future: MCP Tasks for community-CPU long searches

Some JAML filters take minutes to hours of search. Blocking the iframe on that is hostile. The MCP **Tasks** extension (https://modelcontextprotocol.io/extensions/tasks/overview, spec at https://github.com/modelcontextprotocol/experimental-ext-tasks) is the right abstraction: the tool returns a durable `taskId` instead of a result; the client polls `tasks/get` until status reaches `completed`; task IDs survive disconnects.

Runtime fit:
- **Vercel durable workflows** (workflow.dev / Vercel Functions with durable execution) — survives restarts, picks up where it left off, perfect for the polling lifecycle.
- **Cloudflare Durable Objects** — same shape, different vendor.
- **Self-hosted Redis-backed queue + worker** — if community-CPU sharing means volunteer machines, each volunteer runs a worker that pulls jobs off a shared queue and writes results back. The MCP server is the queue's front door.

Don't build this in v0. The v0 server does in-process searches in the iframe, capped at a reasonable `max_results` and a search-time budget. Tasks only enter the picture when (a) there's real demand for huge searches and (b) someone has volunteered CPU. Note in code where the synchronous tool would split into a task-returning tool.

## Reference URLs

- MCP Apps overview: https://modelcontextprotocol.io/extensions/apps/overview
- MCP Apps build guide: https://modelcontextprotocol.io/extensions/apps/build
- Skill repo: https://github.com/modelcontextprotocol/ext-apps
- Examples (color picker, QR code, etc.): https://github.com/modelcontextprotocol/ext-apps/tree/main/examples
- API docs: https://apps.extensions.modelcontextprotocol.io/api/
- Custom connector setup: https://support.anthropic.com/en/articles/11175166-getting-started-with-custom-connectors-using-remote-mcp
- motely-wasm runtime contract: see `X:\jaml-ui\CLAUDE.md` "motely-wasm runtime contract" section
- MCP Tasks: https://modelcontextprotocol.io/extensions/tasks/overview · spec: https://github.com/modelcontextprotocol/experimental-ext-tasks
