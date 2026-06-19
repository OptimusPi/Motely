import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";

/**
 * GET /api/mcp
 * Returns MCP server metadata and available tools.
 */
export async function GET() {
  return Response.json({
    name: "balatro-seed-lab",
    version: "0.1.0",
    description: "Balatro seed search and analysis MCP server",
    tools: [
      {
        name: "search_seeds",
        description: "Plan a JAML-based seed search. The actual search runs client-side via motely-wasm.",
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
        description: "Plan a seed analysis. The actual analysis runs client-side via motely-wasm.",
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
    ],
  });
}

/**
 * POST /api/mcp
 * Handles MCP tool calls. Returns plans that the client executes via motely-wasm.
 */
export async function POST(request: Request) {
  try {
    const body = await request.json();
    const { name, arguments: args } = body;

    switch (name) {
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
        });
      }

      default:
        return Response.json({ error: `Unknown tool: ${name}` }, { status: 400 });
    }
  } catch (err) {
    return Response.json({ error: (err as Error).message }, { status: 500 });
  }
}
