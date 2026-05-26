import { useApp } from "@modelcontextprotocol/ext-apps/react";
import type { CallToolResult } from "@modelcontextprotocol/sdk/types.js";
import { useCallback, useState } from "react";
import { createRoot } from "react-dom/client";
import bootsharp from "motely-wasm";
import { SeedFinderApp } from "./SeedFinderApp";
import { STARTER_JAML } from "./constants";

function extractJaml(result: CallToolResult): string | null {
  if (
    result.structuredContent &&
    typeof result.structuredContent === "object" &&
    "jaml_filter" in result.structuredContent &&
    typeof result.structuredContent.jaml_filter === "string"
  ) {
    return result.structuredContent.jaml_filter;
  }

  const textContent = result.content?.find((part) => part.type === "text");
  if (!textContent || !("text" in textContent)) return null;

  const marker = "jaml_filter:";
  const idx = textContent.text.indexOf(marker);
  if (idx < 0) return null;
  return textContent.text.slice(idx + marker.length).trim();
}

function McpSeedFinder() {
  const [jaml, setJaml] = useState(STARTER_JAML);

  const { app, error } = useApp({
    appInfo: { name: "seed-finder-mcp-app", version: "0.0.0" },
    capabilities: {},
    onAppCreated: (createdApp) => {
      createdApp.ontoolinput = async (input) => {
        const maybeFilter = input.arguments?.jaml_filter;
        if (typeof maybeFilter === "string" && maybeFilter.trim()) {
          setJaml(maybeFilter);
        }
      };

      createdApp.ontoolresult = async (result) => {
        const maybeFilter = extractJaml(result);
        if (maybeFilter) {
          setJaml(maybeFilter);
        }
      };

      createdApp.onerror = async (appError) => {
        console.error("MCP app error", appError);
      };
    },
  });

  const handleRunRequest = useCallback(
    async (nextJaml: string) => {
      if (!app) return;
      await app.callServerTool({
        name: "find_balatro_seeds",
        arguments: {
          jaml_filter: nextJaml,
        },
      });
    },
    [app],
  );

  if (error) {
    return <div style={{ color: "white" }}>MCP connection error: {error.message}</div>;
  }

  return <SeedFinderApp jaml={jaml} onChange={setJaml} onRunRequest={handleRunRequest} />;
}

await bootsharp.boot("/motely-wasm/bin");
createRoot(document.getElementById("root")!).render(<McpSeedFinder />);
