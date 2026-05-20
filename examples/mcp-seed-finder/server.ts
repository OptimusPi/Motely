import { registerAppResource, registerAppTool, RESOURCE_MIME_TYPE } from "@modelcontextprotocol/ext-apps/server";
import { createMcpExpressApp } from "@modelcontextprotocol/sdk/server/express.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import type { CallToolResult, ReadResourceResult } from "@modelcontextprotocol/sdk/types.js";
import cors from "cors";
import express, { type Request, type Response } from "express";
import { existsSync } from "node:fs";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { z } from "zod";

const port = Number(process.env.PORT ?? 3001);
const here = path.dirname(fileURLToPath(import.meta.url));
const distDir = path.join(here, "dist");
const resourceUri = "ui://seed-finder/mcp-app.html";

function findMotelyBin(startDir: string): string | null {
  let dir = startDir;
  while (true) {
    const candidate = path.join(dir, "node_modules", "motely-wasm", "bin");
    if (existsSync(candidate)) return candidate;

    const parent = path.dirname(dir);
    if (parent === dir) return null;
    dir = parent;
  }
}

function createServer(): McpServer {
  const server = new McpServer({
    name: "seed-finder-mcp-example",
    version: "0.0.0",
  });

  registerAppTool(
    server,
    "find_balatro_seeds",
    {
      title: "Find balatro seeds",
      description: "Open the interactive seed finder UI for a JAML filter.",
      inputSchema: {
        jaml_filter: z.string().min(1),
        max_results: z.number().int().positive().max(5000).optional(),
      },
      _meta: { ui: { resourceUri } },
    },
    async ({ jaml_filter, max_results = 100 }): Promise<CallToolResult> => {
      return {
        content: [
          {
            type: "text",
            text: `Seed finder UI opened.\njaml_filter:\n${jaml_filter}\nmax_results: ${max_results}`,
          },
        ],
        structuredContent: {
          jaml_filter,
          max_results,
        },
      };
    },
  );

  registerAppResource(
    server,
    resourceUri,
    resourceUri,
    { mimeType: RESOURCE_MIME_TYPE },
    async (): Promise<ReadResourceResult> => {
      const html = await fs.readFile(path.join(distDir, "mcp-app.html"), "utf-8");
      return {
        contents: [
          {
            uri: resourceUri,
            mimeType: RESOURCE_MIME_TYPE,
            text: html,
            _meta: {
              ui: {
                // Fallback approach for motely assets: keep HTML single-file and
                // allow binary fetches from this same origin.
                csp: {
                  connect_domains: [`http://localhost:${port}`],
                  resource_domains: [`http://localhost:${port}`],
                },
              },
            },
          },
        ],
      };
    },
  );

  return server;
}

async function start() {
  const app = createMcpExpressApp({ host: "0.0.0.0" });
  app.use(cors());
  app.use(express.json({ limit: "1mb" }));

  const motelyBin = findMotelyBin(here);
  if (motelyBin) {
    app.use("/motely-wasm/bin", express.static(motelyBin));
  }

  app.get("/health", (_req, res) => {
    res.json({ ok: true });
  });

  app.all("/mcp", async (req: Request, res: Response) => {
    const server = createServer();
    const transport = new StreamableHTTPServerTransport({
      sessionIdGenerator: undefined,
    });

    res.on("close", () => {
      void transport.close();
      void server.close();
    });

    try {
      await server.connect(transport);
      await transport.handleRequest(req, res, req.body);
    } catch (error) {
      console.error("MCP error:", error);
      if (!res.headersSent) {
        res.status(500).json({
          jsonrpc: "2.0",
          error: { code: -32603, message: "Internal server error" },
          id: null,
        });
      }
    }
  });

  app.listen(port, () => {
    console.log(`Seed finder MCP server listening at http://localhost:${port}/mcp`);
  });
}

void start();
