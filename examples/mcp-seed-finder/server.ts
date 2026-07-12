import { registerAppResource, registerAppTool, RESOURCE_MIME_TYPE } from "@modelcontextprotocol/ext-apps/server";
import { createMcpExpressApp } from "@modelcontextprotocol/sdk/server/express.js";
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import type { CallToolResult, ReadResourceResult } from "@modelcontextprotocol/sdk/types.js";
import cors from "cors";
import express, { type Request, type Response } from "express";
import fs from "node:fs/promises";
import path from "node:path";
import { fileURLToPath } from "node:url";
import { z } from "zod";
import bootsharp, { MotelyJaml, MotelyJamlyzer } from "motely-wasm";
import { bindJimmolateBridge } from "jaml-codemirror";

bindJimmolateBridge();

const port = Number(process.env.PORT ?? 3001);
const here = path.dirname(fileURLToPath(import.meta.url));
const distDir = path.join(here, "dist");
const resourceUri = "ui://seed-finder/mcp-app.html";
const baseUrl = `http://localhost:${port}`;
const appHtmlPath = path.join(distDir, "mcp-app.html");

// Boot the Motely WASM engine once; validation tools run server-side.
await bootsharp.boot();

// Vite emits relative asset paths (e.g. ./assets/index-xxx.js). The MCP App resource
// is rendered in an isolated context, so rewrite them to absolute server URLs.
async function loadAppHtml(): Promise<string> {
  let html = await fs.readFile(appHtmlPath, "utf-8");
  html = html.replace(/(src|href)="\.\/assets\//g, `$1="${baseUrl}/assets/`);
  return html;
}

function createServer(): McpServer {
  const server = new McpServer({
    name: "seed-finder-mcp",
    version: "0.1.0",
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

  registerAppTool(
    server,
    "quick_search",
    {
      title: "Quick seed search",
      description:
        "Easier search: one plain JUMMY line instead of full JAML (e.g. 'Eternal Blueprint in antes 1 or 2'). " +
        "Runs a smaller default sample for a fast result.",
      inputSchema: {
        jummy_line: z.string().min(1),
        sample_size: z.number().int().positive().max(500_000).optional(),
      },
      _meta: { ui: { resourceUri } },
    },
    async ({ jummy_line, sample_size = 10_000 }): Promise<CallToolResult> => {
      const lineError = MotelyJaml.validateLine(jummy_line);
      if (lineError) {
        return {
          content: [{ type: "text", text: `INVALID: ${lineError}` }],
          isError: true,
        };
      }

      return {
        content: [
          {
            type: "text",
            text: `Seed finder UI opened.\njaml_filter:\n${jummy_line}\nmax_results: ${sample_size}`,
          },
        ],
        structuredContent: {
          jaml_filter: jummy_line,
          max_results: sample_size,
        },
      };
    },
  );

  server.tool(
    "jaml_validate",
    "Validate JAML against the real Motely loader — a single clause line (e.g. 'Eternal Blueprint in antes 1 or 2') " +
      "or a full multi-clause filter. JUMMY is JAML; there is no separate format or conversion step. " +
      "Returns OK or the exact loader error.",
    { jaml: z.string() },
    async ({ jaml }): Promise<CallToolResult> => {
      const isSingleLine = !jaml.includes("\n");
      const error = isSingleLine ? MotelyJaml.validateLine(jaml) : MotelyJaml.validate(jaml);
      return {
        content: [
          {
            type: "text",
            text: error ? `INVALID: ${error}` : "OK — valid JAML.",
          },
        ],
        isError: !!error,
      };
    },
  );

  server.tool(
    "analyze_seed",
    "Jamlyzer: run a full ante-by-ante breakdown of one seed against a JAML filter (deck, stake, vouchers, tags, shop/pack contents per ante). Returns the raw analysis result as JSON.",
    {
      seed: z.string().min(1),
      jaml_filter: z.string().min(1),
    },
    async ({ seed, jaml_filter }): Promise<CallToolResult> => {
      const validation = MotelyJaml.validate(jaml_filter);
      if (validation) {
        return {
          content: [{ type: "text", text: `INVALID JAML: ${validation}` }],
          isError: true,
        };
      }

      const config = MotelyJaml.fromYaml(jaml_filter);
      config.seeds = [seed];
      const [result] = MotelyJamlyzer.analyzeSeeds(config);

      return {
        content: [
          {
            type: "text",
            text: JSON.stringify(result, (_key, value) =>
              typeof value === "bigint" ? value.toString() : value,
            ),
          },
        ],
        structuredContent: { seed, result },
      };
    },
  );

  registerAppResource(
    server,
    resourceUri,
    resourceUri,
    { mimeType: RESOURCE_MIME_TYPE },
    async (): Promise<ReadResourceResult> => {
      const html = await loadAppHtml();
      return {
        contents: [
          {
            uri: resourceUri,
            mimeType: RESOURCE_MIME_TYPE,
            text: html,
            _meta: {
              ui: {
                csp: {
                  connect_domains: [baseUrl],
                  resource_domains: [baseUrl],
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
  const server = createServer();
  const app = createMcpExpressApp({ host: "0.0.0.0" });
  app.use(cors());
  app.use(express.json({ limit: "1mb" }));

  // Serve the Vite build output as static assets.
  app.use(express.static(distDir));

  app.get("/health", (_req, res) => {
    res.json({ ok: true });
  });

  app.all("/mcp", async (req: Request, res: Response) => {
    const transport = new StreamableHTTPServerTransport({
      sessionIdGenerator: undefined,
    });

    res.on("close", () => {
      void transport.close();
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
    console.log(`Static assets served from ${distDir}`);
    console.log(`Tools: find_balatro_seeds, jaml_validate, jummy_validate`);
    console.log(`App resource: ${resourceUri}`);
  });
}

void start();
