import { useState, useEffect, useCallback, useRef } from "react";
import { Client } from "@modelcontextprotocol/sdk/client/index.js";
import { StreamableHTTPClientTransport } from "@modelcontextprotocol/sdk/client/streamableHttp.js";

/**
 * Browser MCP Client for JAML Seed Lab
 *
 * Connects to the local/remote MCP HTTP server and exposes
 * tool calls that can be triggered from CodeMirror or React components.
 */

export type McpTool = {
  name: string;
  description?: string;
  inputSchema?: unknown;
};

export type McpConnectionState =
  | "idle"
  | "connecting"
  | "connected"
  | "error";

export interface McpClientOptions {
  serverUrl: string;
  apiKey?: string;
}

export class McpBrowserClient {
  private client: Client | null = null;
  private transport: StreamableHTTPClientTransport | null = null;
  private state: McpConnectionState = "idle";
  private tools: McpTool[] = [];
  private listeners: Set<(state: McpConnectionState, tools: McpTool[]) => void> = new Set();

  constructor(private options: McpClientOptions) {}

  getState() {
    return this.state;
  }

  getTools() {
    return this.tools;
  }

  onChange(cb: (state: McpConnectionState, tools: McpTool[]) => void) {
    this.listeners.add(cb);
    return () => this.listeners.delete(cb);
  }

  private notify() {
    this.listeners.forEach((cb) => cb(this.state, this.tools));
  }

  async connect() {
    if (this.state === "connected" || this.state === "connecting") return;
    this.state = "connecting";
    this.notify();

    try {
      this.transport = new StreamableHTTPClientTransport(
        new URL(this.options.serverUrl),
        {
          authProvider: this.options.apiKey
            ? {
                tokens: () =>
                  Promise.resolve({
                    access_token: this.options.apiKey!,
                    refresh_token: "",
                    expires_in: 3600,
                    token_type: "Bearer",
                  }),
              }
            : undefined,
        }
      );

      this.client = new Client(
        { name: "jaml-seed-lab-ide", version: "0.1.0" },
        { capabilities: {} }
      );

      await this.client.connect(this.transport);

      const toolsResult = await this.client.listTools();
      this.tools = toolsResult.tools.map((t) => ({
        name: t.name,
        description: t.description,
        inputSchema: t.inputSchema,
      }));

      this.state = "connected";
      this.notify();
    } catch (err) {
      console.error("MCP connect failed:", err);
      this.state = "error";
      this.notify();
    }
  }

  async callTool(name: string, args: Record<string, unknown>) {
    if (!this.client || this.state !== "connected") {
      throw new Error("MCP not connected");
    }
    const result = await this.client.callTool({ name, arguments: args });
    return result;
  }

  async disconnect() {
    if (this.transport) {
      await this.transport.close();
    }
    this.client = null;
    this.transport = null;
    this.state = "idle";
    this.tools = [];
    this.notify();
  }
}

/**
 * React hook for the MCP browser client.
 */
export function useMcpClient(options: McpClientOptions) {
  const [state, setState] = useState<McpConnectionState>("idle");
  const [tools, setTools] = useState<McpTool[]>([]);
  const clientRef = useRef<McpBrowserClient | null>(null);

  useEffect(() => {
    const client = new McpBrowserClient(options);
    clientRef.current = client;

    const unsub = client.onChange((s, t) => {
      setState(s);
      setTools(t);
    });

    return () => {
      unsub();
      client.disconnect();
      clientRef.current = null;
    };
  }, [options.serverUrl, options.apiKey]);

  const connect = useCallback(() => clientRef.current?.connect(), []);
  const disconnect = useCallback(() => clientRef.current?.disconnect(), []);

  const callTool = useCallback(
    async (name: string, args: Record<string, unknown>) => {
      if (!clientRef.current) throw new Error("MCP client not initialized");
      return clientRef.current.callTool(name, args);
    },
    []
  );

  return { state, tools, connect, disconnect, callTool, client: clientRef.current };
}
