/**
 * July 2026 SEP POC — MCP Server with ui:// resource extensions.
 *
 * GET  /api/sep-mcp  → Server metadata, tools, and ui:// resources.
 * POST /api/sep-mcp  → Tool calls (including ui_read for ui:// resources).
 */

import {
  buildConnectionSpec,
  buildToolListSpec,
  buildLoadingSpec,
  buildSeedResultsSpec,
} from '@/src/sep-poc/SepPocSpecBuilder';

// ── GET: Server metadata ──

export async function GET() {
  return Response.json({
    name: "july-2026-sep-poc",
    version: "0.1.0",
    description: "July 2026 SEP POC — MCP server with ui:// resources",
    resources: [
      {
        uri: "ui://sep-poc/connection-panel",
        name: "Connection Panel",
        mimeType: "application/vnd.json-render+json",
        description: "Connection status UI panel rendered via json-render",
      },
      {
        uri: "ui://sep-poc/tool-list",
        name: "Tool List",
        mimeType: "application/vnd.json-render+json",
        description: "Available tools UI panel rendered via json-render",
      },
    ],
    tools: [
      {
        name: "search_seeds",
        description: "Plan a JAML-based seed search. Client-side execution via motely-wasm.",
        parameters: {
          type: "object",
          properties: {
            jaml: { type: "string", description: "JAML filter string" },
            seed_count: { type: "number", description: "Number of seeds to search (max 1M)", default: 100000 },
          },
          required: ["jaml"],
        },
      },
      {
        name: "analyze_seed",
        description: "Plan a seed analysis. Client-side execution via motely-wasm.",
        parameters: {
          type: "object",
          properties: {
            seed: { type: "string", description: "Balatro seed code" },
            deck: { type: "string", description: "Starting deck", default: "Red" },
            stake: { type: "string", description: "Stake difficulty", default: "White" },
          },
          required: ["seed"],
        },
      },
      {
        name: "analyze_erratic",
        description: "Analyze a seed's erratic deck composition.",
        parameters: {
          type: "object",
          properties: {
            seed: { type: "string", description: "Balatro seed code" },
          },
          required: ["seed"],
        },
      },
      {
        name: "ui_read",
        description: "Read a ui:// resource and return its json-render spec.",
        parameters: {
          type: "object",
          properties: {
            uri: { type: "string", description: "ui:// URI to read" },
          },
          required: ["uri"],
        },
      },
    ],
  });
}

// ── POST: Tool calls ──

export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { name, arguments: args } = body;

    switch (name) {
      case "ui_read": {
        const uri = args?.uri;
        if (!uri || typeof uri !== "string") {
          return Response.json({ error: "Missing uri" }, { status: 400 });
        }
        return handleUiRead(uri);
      }

      case "search_seeds": {
        const jaml = args?.jaml;
        const seedCount = Math.min(Math.max(1, parseInt(args?.seed_count ?? "100000", 10)), 1_000_000);
        if (!jaml || typeof jaml !== "string") {
          return Response.json({ error: "Missing jaml" }, { status: 400 });
        }
        return Response.json({
          status: "planned",
          jaml,
          seedCount,
          message: "Search planned. Execute client-side via motely-wasm.",
          // ui:// extension: include a suggested resource to render
          uiResource: "ui://sep-poc/search-results",
        });
      }

      case "analyze_seed": {
        const seed = args?.seed;
        const deck = args?.deck ?? "Red";
        const stake = args?.stake ?? "White";
        if (!seed || typeof seed !== "string") {
          return Response.json({ error: "Missing seed" }, { status: 400 });
        }
        return Response.json({
          status: "planned",
          seed,
          deck,
          stake,
          message: "Analysis planned. Execute client-side via motely-wasm.",
          uiResource: "ui://sep-poc/analyze-result",
        });
      }

      case "analyze_erratic": {
        const seed = args?.seed;
        if (!seed || typeof seed !== "string") {
          return Response.json({ error: "Missing seed" }, { status: 400 });
        }
        return Response.json({
          status: "planned",
          seed,
          mode: "erratic",
          message: "Erratic analysis planned. Execute client-side via motely-wasm.",
          uiResource: "ui://sep-poc/erratic-result",
        });
      }

      default:
        return Response.json({ error: `Unknown tool: ${name}` }, { status: 400 });
    }
  } catch (err) {
    return Response.json({ error: (err as Error).message }, { status: 500 });
  }
}

// ── ui:// resource handlers ──

function handleUiRead(uri: string) {
  if (uri === "ui://sep-poc/connection-panel") {
    return Response.json({
      uri,
      spec: buildConnectionSpec("connected", 3, 2),
    });
  }

  if (uri === "ui://sep-poc/tool-list") {
    return Response.json({
      uri,
      spec: buildToolListSpec([
        { name: "search_seeds", description: "Search seeds with JAML filters" },
        { name: "analyze_seed", description: "Analyze a single seed" },
        { name: "analyze_erratic", description: "Erratic deck composition" },
      ]),
    });
  }

  if (uri === "ui://sep-poc/search-results") {
    return Response.json({
      uri,
      spec: buildSeedResultsSpec([
        { seed: "XEQH7CP9", score: 42000 },
        { seed: "ALEEB123", score: 38000 },
      ]),
    });
  }

  if (uri === "ui://sep-poc/analyze-result") {
    return Response.json({
      uri,
      spec: buildLoadingSpec("Analysis complete. Render result here."),
    });
  }

  if (uri === "ui://sep-poc/erratic-result") {
    return Response.json({
      uri,
      spec: buildLoadingSpec("Erratic analysis complete. Render result here."),
    });
  }

  return Response.json({ error: `Unknown ui:// resource: ${uri}` }, { status: 404 });
}
