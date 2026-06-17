import { StateField, StateEffect, Facet } from "@codemirror/state";
import {
  EditorView,
  ViewPlugin,
  ViewUpdate,
  Decoration,
  DecorationSet,
  WidgetType,
  Panel,
  showPanel,
  keymap,
} from "@codemirror/view";
import { McpBrowserClient, McpTool } from "./client.js";

/**
 * CodeMirror 6 MCP Extension for JAML IDE
 *
 * Provides:
 * - StateField for MCP connection status and tools
 * - Effect for executing MCP tools
 * - Decoration widgets for tool results inline in the editor
 * - Keybindings for quick MCP actions
 * - Status panel showing connection state and available tools
 */

// ── Facets ──

export const mcpClientFacet = Facet.define<McpBrowserClient | null, McpBrowserClient | null>({
  combine: (values) => values[0] ?? null,
});

// ── State Effects ──

export const setMcpState = StateEffect.define<{
  state: "idle" | "connecting" | "connected" | "error";
  tools: McpTool[];
}>();

export const addMcpResult = StateEffect.define<{
  id: string;
  tool: string;
  args: Record<string, unknown>;
  result: unknown;
  error?: string;
}>();

export const clearMcpResults = StateEffect.define<null>();

// ── State Field ──

interface McpState {
  connectionState: "idle" | "connecting" | "connected" | "error";
  tools: McpTool[];
  results: Array<{
    id: string;
    tool: string;
    args: Record<string, unknown>;
    result: unknown;
    error?: string;
    line: number;
  }>;
}

const mcpStateField = StateField.define<McpState>({
  create() {
    return { connectionState: "idle", tools: [], results: [] };
  },
  update(state, tr) {
    let newState = state;
    for (const e of tr.effects) {
      if (e.is(setMcpState)) {
        newState = {
          ...newState,
          connectionState: e.value.state,
          tools: e.value.tools,
        };
      } else if (e.is(addMcpResult)) {
        const line = tr.newDoc.lineAt(tr.newDoc.length).number;
        newState = {
          ...newState,
          results: [
            ...newState.results,
            { ...e.value, line },
          ],
        };
      } else if (e.is(clearMcpResults)) {
        newState = { ...newState, results: [] };
      }
    }
    return newState;
  },
});

// ── Result Widget ──

class McpResultWidget extends WidgetType {
  constructor(
    private result: {
      id: string;
      tool: string;
      result: unknown;
      error?: string;
    }
  ) {
    super();
  }

  eq(other: McpResultWidget): boolean {
    return other.result.id === this.result.id;
  }

  toDOM() {
    const el = document.createElement("div");
    el.className = "cm-mcp-result";
    el.style.cssText = `
      margin: 4px 0;
      padding: 8px 12px;
      border-radius: 6px;
      border: 1px solid var(--j-border, #333);
      background: var(--j-surface-muted, #1a1a1a);
      font-family: var(--j-font-code, monospace);
      font-size: 12px;
      line-height: 1.5;
      color: var(--j-foreground, #fff);
      white-space: pre-wrap;
      word-break: break-word;
      max-height: 200px;
      overflow: auto;
    `;

    const header = document.createElement("div");
    header.style.cssText = `
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 4px;
      font-weight: 600;
      font-size: 11px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    `;

    const badge = document.createElement("span");
    badge.textContent = this.result.error ? "❌ ERROR" : "✓ MCP";
    badge.style.cssText = `
      padding: 2px 6px;
      border-radius: 4px;
      background: ${this.result.error ? "#ff6b6b22" : "#6bff6b22"};
      color: ${this.result.error ? "#ff6b6b" : "#6bff6b"};
    `;

    const toolName = document.createElement("span");
    toolName.textContent = this.result.tool;
    toolName.style.color = "var(--j-accent, #0093ff)";

    header.appendChild(badge);
    header.appendChild(toolName);
    el.appendChild(header);

    const body = document.createElement("pre");
    body.style.cssText = "margin: 0; font-size: 11px;";
    if (this.result.error) {
      body.textContent = this.result.error;
      body.style.color = "#ff6b6b";
    } else {
      body.textContent = JSON.stringify(this.result.result, null, 2);
    }
    el.appendChild(body);

    return el;
  }

  ignoreEvent() {
    return false;
  }
}

// ── Decorations ──

function buildResultDecorations(state: McpState): DecorationSet {
  const builder = new RangeSetBuilder<Decoration>();
  for (const result of state.results) {
    const line = state.results.indexOf(result) + 1; // approximate line
    // In real implementation, we'd track the actual line position
    // For now, decorate at the end of the document
    const pos = state.results.length * 1000; // placeholder
    builder.add(
      pos,
      pos,
      Decoration.widget({
        widget: new McpResultWidget(result),
        side: 1,
        block: true,
      })
    );
  }
  return builder.finish();
}

import { RangeSetBuilder } from "@codemirror/state";

const mcpResultDecorations = ViewPlugin.fromClass(
  class {
    decorations: DecorationSet;
    constructor(view: EditorView) {
      this.decorations = buildResultDecorations(view.state.field(mcpStateField));
    }
    update(update: ViewUpdate) {
      if (update.state.field(mcpStateField) !== update.startState.field(mcpStateField)) {
        this.decorations = buildResultDecorations(update.state.field(mcpStateField));
      }
    }
  },
  {
    decorations: (v) => v.decorations,
  }
);

// ── Status Panel ──

class McpStatusPanel implements Panel {
  dom: HTMLElement;
  private view: EditorView;

  constructor(view: EditorView) {
    this.view = view;
    this.dom = document.createElement("div");
    this.dom.className = "cm-mcp-status-panel";
    this.dom.style.cssText = `
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 4px 12px;
      font-size: 12px;
      font-family: var(--j-font-code, monospace);
      border-top: 1px solid var(--j-border, #333);
      background: var(--j-surface, #111);
      color: var(--j-muted, #888);
    `;
    this.update();
  }

  update() {
    const mcp = this.view.state.field(mcpStateField);
    const client = this.view.state.facet(mcpClientFacet);

    const statusColor =
      mcp.connectionState === "connected"
        ? "#6bff6b"
        : mcp.connectionState === "error"
        ? "#ff6b6b"
        : mcp.connectionState === "connecting"
        ? "#e4b643"
        : "var(--j-muted, #888)";

    const toolList = mcp.tools
      .map((t) => `<span style="color:var(--j-accent,#0093ff);cursor:pointer;" data-tool="${t.name}">${t.name}</span>`)
      .join(", ");

    this.dom.innerHTML = `
      <span style="display:flex;align-items:center;gap:6px;">
        <span style="width:8px;height:8px;border-radius:50%;background:${statusColor};display:inline-block;"></span>
        <span style="color:${statusColor};font-weight:600;text-transform:uppercase;font-size:10px;letter-spacing:0.05em;">
          ${mcp.connectionState}
        </span>
      </span>
      <span style="color:var(--j-muted);">|</span>
      <span style="display:flex;align-items:center;gap:6px;flex-wrap:wrap;">
        <span style="color:var(--j-muted);">Tools:</span>
        ${mcp.tools.length > 0 ? toolList : "<span style=\"color:var(--j-muted);\">none</span>"}
      </span>
      <span style="margin-left:auto;display:flex;gap:8px;">
        ${mcp.connectionState === "idle" || mcp.connectionState === "error"
          ? `<button class="cm-mcp-connect" style="background:var(--j-accent,#0093ff);color:#000;border:none;border-radius:4px;padding:2px 8px;font-size:11px;font-weight:600;cursor:pointer;">Connect</button>`
          : mcp.connectionState === "connected"
          ? `<button class="cm-mcp-disconnect" style="background:var(--j-surface-muted);color:var(--j-foreground);border:1px solid var(--j-border);border-radius:4px;padding:2px 8px;font-size:11px;cursor:pointer;">Disconnect</button>`
          : `<span style="color:#e4b643;font-size:11px;">Connecting…</span>`
        }
      </span>
    `;

    // Wire up buttons
    const connectBtn = this.dom.querySelector(".cm-mcp-connect") as HTMLButtonElement | null;
    const disconnectBtn = this.dom.querySelector(".cm-mcp-disconnect") as HTMLButtonElement | null;

    connectBtn?.addEventListener("click", () => {
      client?.connect();
    });

    disconnectBtn?.addEventListener("click", () => {
      client?.disconnect();
    });

    // Wire up tool clicks
    this.dom.querySelectorAll("[data-tool]").forEach((el) => {
      el.addEventListener("click", () => {
        const toolName = (el as HTMLElement).dataset.tool!;
        this.executeTool(toolName);
      });
    });
  }

  private async executeTool(toolName: string) {
    const client = this.view.state.facet(mcpClientFacet);
    if (!client) return;

    const mcp = this.view.state.field(mcpStateField);
    const tool = mcp.tools.find((t) => t.name === toolName);
    if (!tool) return;

    // Extract args from current JAML content
    const jaml = this.view.state.doc.toString();
    let args: Record<string, unknown> = {};

    if (toolName === "search_seeds") {
      args = { jaml, seed_count: 100000 };
    } else if (toolName === "analyze_seed") {
      // Try to extract seed from JAML
      const seedMatch = jaml.match(/seed:\s*(\S+)/);
      args = { seed: seedMatch?.[1] ?? "XEQH7CP9", deck: "Red", stake: "White" };
    } else if (toolName === "analyze_erratic") {
      const seedMatch = jaml.match(/seed:\s*(\S+)/);
      args = { seed: seedMatch?.[1] ?? "XEQH7CP9" };
    }

    try {
      const result = await client.callTool(toolName, args);
      this.view.dispatch({
        effects: addMcpResult.of({
          id: `${toolName}-${Date.now()}`,
          tool: toolName,
          args,
          result,
        }),
      });
    } catch (err) {
      this.view.dispatch({
        effects: addMcpResult.of({
          id: `${toolName}-${Date.now()}`,
          tool: toolName,
          args,
          result: null,
          error: (err as Error).message,
        }),
      });
    }
  }

  destroy() {
    // cleanup
  }
}

function createMcpStatusPanel(view: EditorView): Panel {
  return new McpStatusPanel(view);
}

// ── Keybindings ──

const mcpKeymap = keymap.of([
  {
    key: "Mod-Shift-M",
    run: (view) => {
      const client = view.state.facet(mcpClientFacet);
      client?.connect();
      return true;
    },
  },
  {
    key: "Mod-Shift-R",
    run: (view) => {
      view.dispatch({ effects: clearMcpResults.of(null) });
      return true;
    },
  },
]);

// ── Main Extension ──

/**
 * Create the CodeMirror 6 MCP extension.
 *
 * Usage:
 * ```ts
 * import { EditorView } from "@codemirror/view";
 * import { mcpExtension } from "jaml-seed-lab/codemirror";
 * import { McpBrowserClient } from "jaml-seed-lab/mcp";
 *
 * const client = new McpBrowserClient({ serverUrl: "http://localhost:3000/api/mcp" });
 * const view = new EditorView({
 *   extensions: [mcpExtension(client)],
 *   parent: document.body,
 * });
 * ```
 */
export function mcpExtension(client: McpBrowserClient) {
  // Listen to client state changes and sync to CodeMirror state
  client.onChange((state, tools) => {
    // This would need access to the EditorView to dispatch
    // In practice, the React component wires this up
  });

  return [
    mcpClientFacet.of(client),
    mcpStateField,
    mcpResultDecorations,
    showPanel.of(createMcpStatusPanel),
    mcpKeymap,
  ];
}

// Re-exports
export { mcpStateField, addMcpResult, clearMcpResults, setMcpState };
export type { McpState };
