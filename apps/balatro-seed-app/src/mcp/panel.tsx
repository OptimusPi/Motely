import { useState, useCallback, useMemo } from "react";
import {
  Renderer,
  StateProvider,
  ActionProvider,
  VisibilityProvider,
  ValidationProvider,
} from "@json-render/react";
import { registry } from "@/lib/registry";
import { buildLoadingSpec, buildChatSpec } from "@/lib/spec-builder";
import type { SpecType } from "@json-render/react";
import { useMcpClient, McpTool } from "./client";

/**
 * MCP Panel — React component for the IDE's MCP integration.
 *
 * Shows:
 * - Connection status (connect/disconnect buttons)
 * - Available tools list with execute buttons
 * - Tool execution results rendered via json-render
 * - Chat-style interaction log
 */

interface McpPanelProps {
  serverUrl?: string;
  apiKey?: string;
  jaml: string;
}

export function McpPanel({ serverUrl = "/api/mcp", apiKey, jaml }: McpPanelProps) {
  const { state, tools, connect, disconnect, callTool } = useMcpClient({
    serverUrl,
    apiKey,
  });

  const [executing, setExecuting] = useState<string | null>(null);
  const [results, setResults] = useState<
    Array<{
      id: string;
      tool: string;
      args: Record<string, unknown>;
      result: unknown;
      error?: string;
      timestamp: string;
    }>
  >([]);

  const handleExecute = useCallback(
    async (tool: McpTool) => {
      setExecuting(tool.name);

      let args: Record<string, unknown> = {};
      if (tool.name === "search_seeds") {
        args = { jaml, seed_count: 100000 };
      } else if (tool.name === "analyze_seed") {
        const seedMatch = jaml.match(/seed:\s*(\S+)/);
        args = {
          seed: seedMatch?.[1] ?? "XEQH7CP9",
          deck: "Red",
          stake: "White",
        };
      } else if (tool.name === "analyze_erratic") {
        const seedMatch = jaml.match(/seed:\s*(\S+)/);
        args = { seed: seedMatch?.[1] ?? "XEQH7CP9" };
      }

      try {
        const result = await callTool(tool.name, args);
        setResults((prev) => [
          ...prev,
          {
            id: `${tool.name}-${Date.now()}`,
            tool: tool.name,
            args,
            result,
            timestamp: new Date().toLocaleTimeString(),
          },
        ]);
      } catch (err) {
        setResults((prev) => [
          ...prev,
          {
            id: `${tool.name}-${Date.now()}`,
            tool: tool.name,
            args,
            result: null,
            error: (err as Error).message,
            timestamp: new Date().toLocaleTimeString(),
          },
        ]);
      } finally {
        setExecuting(null);
      }
    },
    [callTool, jaml]
  );

  const clearResults = useCallback(() => setResults([]), []);

  // Build spec from results
  const spec = useMemo<SpecType>(() => {
    if (results.length === 0) {
      return buildLoadingSpec(
        state === "connected"
          ? "Click a tool to execute it. Results appear here."
          : state === "connecting"
          ? "Connecting to MCP server…"
          : "Connect to the MCP server to execute tools."
      );
    }

    const messages = results.map((r) => ({
      role: r.error ? ("assistant" as const) : ("assistant" as const),
      content: r.error
        ? `Error executing ${r.tool}: ${r.error}`
        : `${r.tool} executed successfully.\n\n${JSON.stringify(r.result, null, 2)}`,
      timestamp: r.timestamp,
    }));

    return buildChatSpec(messages);
  }, [results, state]);

  const statusColor =
    state === "connected"
      ? "#6bff6b"
      : state === "error"
      ? "#ff6b6b"
      : state === "connecting"
      ? "#e4b643"
      : "var(--j-muted)";

  return (
    <div className="flex flex-col gap-4">
      {/* Connection Bar */}
      <div
        className="flex items-center justify-between rounded-lg border p-3"
        style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
      >
        <div className="flex items-center gap-3">
          <span
            className="inline-block h-2.5 w-2.5 rounded-full"
            style={{ backgroundColor: statusColor }}
          />
          <span
            className="text-sm font-semibold uppercase"
            style={{ color: statusColor, letterSpacing: "0.05em" }}
          >
            {state}
          </span>
          <span className="text-xs" style={{ color: "var(--j-muted)" }}>
            {tools.length > 0 ? `${tools.length} tools available` : ""}
          </span>
        </div>
        <div className="flex gap-2">
          {state === "idle" || state === "error" ? (
            <button
              className="rounded px-3 py-1.5 text-xs font-semibold"
              style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
              onClick={connect}
            >
              Connect
            </button>
          ) : state === "connected" ? (
            <button
              className="rounded px-3 py-1.5 text-xs font-semibold"
              style={{
                border: "1px solid var(--j-border)",
                color: "var(--j-muted)",
              }}
              onClick={disconnect}
            >
              Disconnect
            </button>
          ) : (
            <span className="text-xs" style={{ color: "#e4b643" }}>
              Connecting…
            </span>
          )}
        </div>
      </div>

      {/* Tools Grid */}
      {tools.length > 0 && (
        <div className="grid grid-cols-1 gap-2">
          {tools.map((tool) => (
            <div
              key={tool.name}
              className="flex items-center justify-between rounded border p-3"
              style={{ borderColor: "var(--j-border)" }}
            >
              <div className="flex flex-col gap-1">
                <span
                  className="font-semibold text-sm"
                  style={{ color: "var(--j-foreground)" }}
                >
                  {tool.name}
                </span>
                {tool.description && (
                  <span className="text-xs" style={{ color: "var(--j-muted)" }}>
                    {tool.description}
                  </span>
                )}
              </div>
              <button
                className="rounded px-3 py-1.5 text-xs font-semibold"
                style={{
                  backgroundColor:
                    executing === tool.name
                      ? "var(--j-surface-muted)"
                      : "var(--j-accent)",
                  color: executing === tool.name ? "var(--j-muted)" : "#000",
                }}
                onClick={() => handleExecute(tool)}
                disabled={executing !== null}
              >
                {executing === tool.name ? "Running…" : "Execute"}
              </button>
            </div>
          ))}
        </div>
      )}

      {/* Results */}
      {results.length > 0 && (
        <div className="flex flex-col gap-2">
          <div className="flex items-center justify-between">
            <span className="text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
              Results ({results.length})
            </span>
            <button
              className="rounded px-2 py-1 text-xs"
              style={{ border: "1px solid var(--j-border)", color: "var(--j-muted)" }}
              onClick={clearResults}
            >
              Clear
            </button>
          </div>
          <div
            className="rounded-lg border p-4"
            style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
          >
            <StateProvider initialState={{}}>
              <VisibilityProvider>
                <ActionProvider handlers={{}}>
                  <ValidationProvider>
                    <Renderer spec={spec} registry={registry} />
                  </ValidationProvider>
                </ActionProvider>
              </VisibilityProvider>
            </StateProvider>
          </div>
        </div>
      )}
    </div>
  );
}

// Simple useMemo since we don't have React imported here
function useMemo<T>(factory: () => T, deps: unknown[]): T {
  const [state, setState] = useState<T>(factory);
  const depsRef = useRef(deps);
  if (!depsRef.current || !deps.every((d, i) => d === depsRef.current[i])) {
    depsRef.current = deps;
    setState(factory);
  }
  return state;
}

import { useRef } from "react";
