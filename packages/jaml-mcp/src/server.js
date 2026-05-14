#!/usr/bin/env node
// jaml-mcp — an MCP server for working with JAML, the filter language of the
// Motely Balatro seed-search engine. Its core job is natural language -> JAML;
// it also validates and explains JAML using the real engine (motely-wasm).
//
// It does not run searches — jaml-ui already does that.
//
// Tools:
//   nl_to_jaml      prime the model with the JAML grammar + examples to author a filter
//   jaml_reference  the JAML authoring guide, whole or by section
//   jaml_examples   curated example JAML filters
//   validate_jaml   parse a JAML document with the real engine; report errors
//   explain_jaml    describe what a JAML document evaluates to
// Prompt:
//   nl_to_jaml      the same priming as the tool, surfaced as an MCP prompt
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

import { loadSchema, buildGuide, buildPrimer, getSections, CURATED_EXAMPLES } from "./guide.js";
import { validateJaml, explainJaml } from "./engine.js";

const PKG_VERSION = "0.1.0";

const { schema, path: schemaPath } = loadSchema();
const GUIDE = buildGuide(schema);
const SECTIONS = getSections(GUIDE);
const SECTION_SLUGS = SECTIONS.map((s) => s.slug);

const NL_TOOL_DESCRIPTION =
  "Translate a natural-language Balatro seed-search request into a JAML filter " +
  "document. Returns the JAML authoring guide (grammar, live enum vocabularies, " +
  "curated examples) followed by the request — produce the JAML yourself from " +
  "that context, then confirm it with validate_jaml. Use this whenever the user " +
  "describes a seed they want in plain English and needs a JAML filter.";

function textResult(text, isError = false) {
  return { content: [{ type: "text", text }], isError };
}

function createServer() {
  const server = new McpServer({ name: "jaml-mcp", version: PKG_VERSION });

  server.registerTool(
    "nl_to_jaml",
    {
      title: "Natural language -> JAML",
      description: NL_TOOL_DESCRIPTION,
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
    async ({ request, deck, stake }) => textResult(buildPrimer(GUIDE, request, { deck, stake })),
  );

  server.registerTool(
    "jaml_reference",
    {
      title: "JAML reference",
      description:
        "The JAML authoring guide: grammar, document/clause shape, naming rules, " +
        "live enum vocabularies, and worked examples. Returns the whole guide, or " +
        `a single section when 'section' is given. Sections: ${SECTION_SLUGS.join(", ")}.`,
      inputSchema: {
        section: z
          .string()
          .optional()
          .describe(`Optional section slug. One of: ${SECTION_SLUGS.join(", ")}.`),
      },
    },
    async ({ section }) => {
      if (!section) return textResult(GUIDE);
      const match = SECTIONS.find((s) => s.slug === section);
      if (match) return textResult(match.body);
      return textResult(
        `Unknown section '${section}'. Available sections: ${SECTION_SLUGS.join(", ")}.`,
        true,
      );
    },
  );

  server.registerTool(
    "jaml_examples",
    {
      title: "JAML examples",
      description:
        "Curated example JAML filter documents, each annotated with what it teaches. " +
        "Returns all examples, or only those matching 'query' (substring match on the " +
        "example name and its teaching note).",
      inputSchema: {
        query: z
          .string()
          .optional()
          .describe("Optional substring filter, e.g. 'sources', 'editions', 'mustNot'."),
      },
    },
    async ({ query }) => {
      const q = query?.trim().toLowerCase();
      const picked = q
        ? CURATED_EXAMPLES.filter(
            (e) =>
              e.name.toLowerCase().includes(q) || e.teaches.toLowerCase().includes(q),
          )
        : CURATED_EXAMPLES;
      if (!picked.length) {
        return textResult(
          `No examples match '${query}'. Try one of: ` +
            CURATED_EXAMPLES.map((e) => e.name).join("; "),
          true,
        );
      }
      const rendered = picked
        .map((e) => `### ${e.name}\n_Teaches: ${e.teaches}_\n\n\`\`\`yaml\n${e.jaml}\n\`\`\``)
        .join("\n\n");
      return textResult(rendered);
    },
  );

  server.registerTool(
    "validate_jaml",
    {
      title: "Validate JAML",
      description:
        "Parse a JAML document with the real Motely engine and report whether it is " +
        "valid. On failure, returns the engine's parse/build error message. Use this " +
        "to confirm any JAML you author.",
      inputSchema: {
        jaml: z.string().describe("The full JAML document to validate."),
      },
    },
    async ({ jaml }) => {
      try {
        const { ok, message } = await validateJaml(jaml);
        return textResult(ok ? "valid" : `invalid: ${message}`, !ok);
      } catch (err) {
        return textResult(`validate_jaml failed to run the engine: ${err?.message ?? err}`, true);
      }
    },
  );

  server.registerTool(
    "explain_jaml",
    {
      title: "Explain JAML",
      description:
        "Describe what a JAML document evaluates to — the engine's filter eval plan: " +
        "must/should/mustNot clauses, their order, and per-clause cost. Returns the " +
        "validation error instead if the document does not parse.",
      inputSchema: {
        jaml: z.string().describe("The full JAML document to explain."),
      },
    },
    async ({ jaml }) => {
      try {
        const { ok, message, explanation } = await explainJaml(jaml);
        return textResult(ok ? explanation : `invalid: ${message}`, !ok);
      } catch (err) {
        return textResult(`explain_jaml failed to run the engine: ${err?.message ?? err}`, true);
      }
    },
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
