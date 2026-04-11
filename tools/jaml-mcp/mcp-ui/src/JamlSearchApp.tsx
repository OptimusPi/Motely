import type { App } from "@modelcontextprotocol/ext-apps";
import { useApp } from "@modelcontextprotocol/ext-apps/react";
import {
  ActionProvider,
  Renderer,
  StateProvider,
} from "@json-render/react";
import { useCallback, useRef, useState } from "react";
import {
  buildErrorSpec,
  buildSpecFromSearch,
  registry,
} from "./registry.js";
import type { SearchResponse } from "./searchTypes.js";

function parseSearchResultPayload(result: {
  content?: Array<{ type: string; text?: string }>;
  structuredContent?: unknown;
}): SearchResponse {
  const structured = result.structuredContent;
  if (structured && typeof structured === "object") {
    const data = structured as SearchResponse;
    if (!Array.isArray(data.results)) data.results = [];
    return data;
  }
  const text = result.content?.find((c) => c.type === "text")?.text;
  if (!text) throw new Error("No structuredContent or text content in tool result.");
  const data = JSON.parse(text) as SearchResponse;
  if (!data || typeof data !== "object") throw new Error("Invalid search payload");
  if (!Array.isArray(data.results)) data.results = [];
  return data;
}

export function JamlSearchApp() {
  const [spec, setSpec] = useState(() =>
    buildErrorSpec("Waiting for search results..."),
  );
  const [loading, setLoading] = useState(false);
  const appRef = useRef<App | null>(null);
  const lastArgsRef = useRef<Record<string, unknown>>({});

  const onAppCreated = useCallback((app: App) => {
    appRef.current = app;

    app.ontoolinput = (input) => {
      if (input?.arguments && typeof input.arguments === "object")
        lastArgsRef.current = input.arguments as Record<string, unknown>;
    };

    app.ontoolresult = (result) => {
      try {
        setSpec(buildSpecFromSearch(parseSearchResultPayload(result)));
      } catch (e) {
        const text = result.content?.find((c) => c.type === "text")?.text ?? "";
        setSpec(
          buildErrorSpec(
            `Could not parse results: ${(e as Error).message}\n\n${text.slice(0, 2000)}`,
          ),
        );
      }
      setLoading(false);
    };
  }, []);

  const { isConnected, error } = useApp({
    appInfo: { name: "jaml-search", version: "1.0.0" },
    capabilities: {},
    onAppCreated,
  });

  const rerunSearch = useCallback(async () => {
    const app = appRef.current;
    if (!app || loading) return;
    setLoading(true);
    const args = lastArgsRef.current;
    try {
      const result = await app.callServerTool({
        name: "search_seeds",
        arguments: args,
      });
      setSpec(buildSpecFromSearch(parseSearchResultPayload(result)));
    } catch (e) {
      setSpec(buildErrorSpec(`Re-roll failed: ${(e as Error).message}`));
    }
    setLoading(false);
  }, [loading]);

  if (error)
    return (
      <div style={{ color: "#ef4444", padding: 12 }}>
        Connection error: {error.message}
      </div>
    );
  if (!isConnected)
    return <div style={{ padding: 12, color: "var(--muted, #64748b)" }}>Connecting...</div>;

  return (
    <StateProvider initialState={{}}>
      <ActionProvider
        handlers={{
          rerunSearch: rerunSearch,
        }}
      >
        {loading && (
          <div
            style={{
              padding: "8px 12px",
              background: "var(--stats-bg, #e2e8f0)",
              borderRadius: 8,
              fontSize: "0.85rem",
              marginBottom: 8,
            }}
          >
            Searching...
          </div>
        )}
        <Renderer spec={spec as never} registry={registry} />
      </ActionProvider>
    </StateProvider>
  );
}
