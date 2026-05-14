#!/usr/bin/env node
// jaml-mcp — an MCP server whose single job is natural language -> JAML.
//
// It does not run searches and it does not validate (jaml-ui already does
// both). It primes the calling model with the JAML grammar, the live enum
// vocabularies, and curated few-shot examples so the model can reliably emit
// a correct JAML filter document from a plain-English request.
//
// Transports:
//   node src/server.js           stdio (default — Claude Code, desktop)
//   node src/server.js --http    Streamable HTTP on PORT (default 3141), for
//                                remote use such as Claude.ai on a phone

import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import express from "express";
import { z } from "zod";

import { loadSchema, buildGuide, buildPrimer } from "./guide.js";

const PKG_VERSION = "0.1.0";

const { schema, path: schemaPath } = loadSchema();
const GUIDE = buildGuide(schema);

const TOOL_DESCRIPTION =
  "Translate a natural-language Balatro seed-search request into a JAML filter " +
  "document. Returns the JAML authoring guide (grammar, live enum vocabularies, " +
  "curated examples) followed by the request — produce the JAML yourself from " +
  "that context. Use this whenever the user describes a seed they want in plain " +
  "English and needs a JAML filter.";

function createServer() {
  const server = new McpServer({ name: "jaml-mcp", version: PKG_VERSION });

  server.registerTool(
    "nl_to_jaml",
    {
      title: "Natural language -> JAML",
      description: TOOL_DESCRIPTION,
      inputSchema: {
        request: z
          .string()
          .describe(
            "The seed-search request in plain English, e.g. 'Magic deck run with a " +
              "Negative Perkeo from a soul card in ante 1, bonus points for an early Blueprint'.",
          ),
        deck: z
          .string()
          .optional()
          .describe("Optional deck hint if the user named one (e.g. Magic, Red, Erratic)."),
        stake: z
          .string()
          .optional()
          .describe("Optional stake hint if the user named one (e.g. White, Gold)."),
      },
    },
    async ({ request, deck, stake }) => ({
      content: [{ type: "text", text: buildPrimer(GUIDE, request, { deck, stake }) }],
    }),
  );

  server.registerPrompt(
    "nl_to_jaml",
    {
      title: "Natural language -> JAML",
      description:
        "Prime the model to translate a plain-English seed-search request into a JAML filter document.",
      argsSchema: {
        request: z.string().describe("The seed-search request in plain English."),
      },
    },
    ({ request }) => ({
      messages: [
        {
          role: "user",
          content: { type: "text", text: buildPrimer(GUIDE, request, {}) },
        },
      ],
    }),
  );

  return server;
}

async function runStdio() {
  const server = createServer();
  const transport = new StdioServerTransport();
  await server.connect(transport);
  // stdout is the JSON-RPC channel — all diagnostics go to stderr.
  console.error(`jaml-mcp ${PKG_VERSION} ready on stdio (schema: ${schemaPath})`);
}

function runHttp() {
  const port = Number(process.env.PORT) || 3141;
  const app = express();
  app.use(express.json());

  app.post("/mcp", async (req, res) => {
    try {
      const server = createServer();
      const transport = new StreamableHTTPServerTransport({ sessionIdGenerator: undefined });
      res.on("close", () => {
        transport.close();
        server.close();
      });
      await server.connect(transport);
      await transport.handleRequest(req, res, req.body);
    } catch (err) {
      console.error("jaml-mcp request error:", err);
      if (!res.headersSent) {
        res.status(500).json({
          jsonrpc: "2.0",
          error: { code: -32603, message: "Internal server error" },
          id: null,
        });
      }
    }
  });

  // Stateless mode: only POST carries JSON-RPC.
  const methodNotAllowed = (_req, res) => {
    res.status(405).json({
      jsonrpc: "2.0",
      error: { code: -32000, message: "Method not allowed." },
      id: null,
    });
  };
  app.get("/mcp", methodNotAllowed);
  app.delete("/mcp", methodNotAllowed);

  app.listen(port, () => {
    console.error(
      `jaml-mcp ${PKG_VERSION} ready on http://localhost:${port}/mcp (schema: ${schemaPath})`,
    );
  });
}

if (process.argv.includes("--http") || process.env.JAML_MCP_HTTP === "1") {
  runHttp();
} else {
  runStdio().catch((err) => {
    console.error("jaml-mcp failed to start:", err);
    process.exit(1);
  });
}
