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
  buildLoadingSpec,
  buildSeedDetailSpec,
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
    buildLoadingSpec("Waiting for search results..."),
  );
  const [loading, setLoading] = useState(false);
  const appRef = useRef<App | null>(null);
  const lastArgsRef = useRef<Record<string, unknown>>({});
  const lastSearchRef = useRef<SearchResponse | null>(null);
  // Track which view: "results" or "detail"
  const [view, setView] = useState<"results" | "detail">("results");

  const onAppCreated = useCallback((app: App) => {
    appRef.current = app;

    app.ontoolinput = (input) => {
      if (input?.arguments && typeof input.arguments === "object")
        lastArgsRef.current = input.arguments as Record<string, unknown>;
    };

    app.ontoolresult = (result) => {
      try {
        const parsed = parseSearchResultPayload(result);
        lastSearchRef.current = parsed;
        const args = lastArgsRef.current;
        setSpec(buildSpecFromSearch(parsed, {
          jummy: args.jummy as string | undefined,
          jaml: args.jaml as string | undefined,
        }));
        setView("results");
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
    setSpec(buildLoadingSpec("Re-rolling search..."));
    const args = lastArgsRef.current;
    try {
      const result = await app.callServerTool({
        name: "search_seeds",
        arguments: args,
      });
      const parsed = parseSearchResultPayload(result);
      lastSearchRef.current = parsed;
      setSpec(buildSpecFromSearch(parsed, {
        jummy: args.jummy as string | undefined,
        jaml: args.jaml as string | undefined,
      }));
      setView("results");
    } catch (e) {
      setSpec(buildErrorSpec(`Re-roll failed: ${(e as Error).message}`));
    }
    setLoading(false);
  }, [loading]);

  const analyzeSeed = useCallback(async (_params: unknown, eventData?: { seed?: string }) => {
    const app = appRef.current;
    const seed = eventData?.seed;
    if (!app || !seed) return;

    setView("detail");
    setSpec(buildSeedDetailSpec(seed, { loading: true }));

    // Build minimal JAML from last search args for deck/stake
    const args = lastArgsRef.current;
    const jaml = (args.jaml as string) || '{"deck":"Red","stake":"White"}';

    try {
      const result = await app.callServerTool({
        name: "analyze_seed",
        arguments: { seed, jaml },
      });
      const text = result.content?.find((c: { type: string }) => c.type === "text")?.text ?? "{}";
      setSpec(buildSeedDetailSpec(seed, { analysisJson: text }));
    } catch (e) {
      setSpec(buildSeedDetailSpec(seed, { error: (e as Error).message }));
    }
  }, []);

  const closeSeedDetail = useCallback(() => {
    setView("results");
    if (lastSearchRef.current) {
      const args = lastArgsRef.current;
      setSpec(buildSpecFromSearch(lastSearchRef.current, {
        jummy: args.jummy as string | undefined,
        jaml: args.jaml as string | undefined,
      }));
    } else {
      setSpec(buildLoadingSpec("Waiting for search results..."));
    }
  }, []);

  if (error)
    return (
      <div style={{ color: "#ef4444", padding: 12 }}>
        Connection error: {error.message}
      </div>
    );
  if (!isConnected)
    return (
      <div style={{ display: "flex", alignItems: "center", gap: 10, padding: 12, color: "var(--text2, #a0a8b8)" }}>
        <div
          style={{
            width: 16,
            height: 16,
            border: "2px solid var(--border, #334461)",
            borderTopColor: "var(--blue, #0093ff)",
            borderRadius: "50%",
            animation: "spin 0.8s linear infinite",
          }}
        />
        Connecting...
      </div>
    );

  return (
    <StateProvider initialState={{}}>
      <ActionProvider
        handlers={{
          rerunSearch,
          analyzeSeed,
          closeSeedDetail,
        }}
      >
        <Renderer spec={spec as never} registry={registry} />
      </ActionProvider>
    </StateProvider>
  );
}
