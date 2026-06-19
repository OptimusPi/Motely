"use client";

import { useEffect, useRef, useState } from "react";
import { JamlIdePage } from "../apps/ide";

/**
 * MCP App Wrapper — for @modelcontextprotocol/ext-apps integration.
 *
 * When the JAML IDE is embedded as an MCP App (iframe), this wrapper
 * handles the postMessage protocol with the parent MCP client.
 *
 * The parent sends:
 *   - `{ type: "tool-call", tool: "search_seeds", arguments: { jaml: "..." } }`
 *   - `{ type: "get-content" }` — returns current JAML
 *   - `{ type: "set-content", content: "..." }` — sets JAML
 *
 * The child sends:
 *   - `{ type: "content-change", content: "..." }`
 *   - `{ type: "tool-result", tool: "...", result: ... }`
 *   - `{ type: "ready" }`
 */

export function McpAppWrapper() {
  const [jaml, setJaml] = useState("");
  const parentRef = useRef<Window | null>(null);
  const readySent = useRef(false);

  useEffect(() => {
    // Check if we're inside an iframe (MCP app mode)
    if (window.parent === window) {
      // Not in iframe, render as standalone app
      return;
    }

    parentRef.current = window.parent;

    // Send ready signal
    if (!readySent.current) {
      window.parent.postMessage(
        { type: "ready", app: "jaml-seed-lab-ide", version: "0.1.0" },
        "*"
      );
      readySent.current = true;
    }

    const handleMessage = (event: MessageEvent) => {
      const data = event.data;
      if (!data || typeof data !== "object") return;

      switch (data.type) {
        case "set-content": {
          if (data.content && typeof data.content === "string") {
            setJaml(data.content);
          }
          break;
        }
        case "get-content": {
          window.parent.postMessage(
            { type: "content", content: jaml },
            "*"
          );
          break;
        }
        case "tool-call": {
          // Tool calls are forwarded to the API
          handleToolCall(data.tool, data.arguments);
          break;
        }
      }
    };

    window.addEventListener("message", handleMessage);
    return () => window.removeEventListener("message", handleMessage);
  }, [jaml]);

  const handleJamlChange = (newJaml: string) => {
    setJaml(newJaml);
    if (parentRef.current && parentRef.current !== window) {
      parentRef.current.postMessage(
        { type: "content-change", content: newJaml },
        "*"
      );
    }
  };

  const handleToolCall = async (
    tool: string,
    args: Record<string, unknown>
  ) => {
    try {
      const response = await fetch("/api/mcp", {
        method: "POST",
        headers: { "Content-Type": "application/json" },
        body: JSON.stringify({ name: tool, arguments: args }),
      });
      const result = await response.json();

      if (parentRef.current && parentRef.current !== window) {
        parentRef.current.postMessage(
          { type: "tool-result", tool, result },
          "*"
        );
      }
    } catch (err) {
      if (parentRef.current && parentRef.current !== window) {
        parentRef.current.postMessage(
          {
            type: "tool-error",
            tool,
            error: (err as Error).message,
          },
          "*"
        );
      }
    }
  };

  // In iframe mode, we render a simplified IDE
  if (window.parent !== window) {
    return (
      <div className="h-screen w-screen overflow-hidden" style={{ background: "var(--j-dark-grey)" }}>
        <JamlIdePage />
      </div>
    );
  }

  // Standalone mode: just render the normal IDE
  return <JamlIdePage />;
}
