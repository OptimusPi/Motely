/**
 * POST /api/search
 *
 * Public demo endpoint — no auth required.
 * Body: { jaml: string, seed_count?: number }
 * Returns: SearchResponse JSON
 */
import { searchSeeds } from "./tools.js";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

const MAX_DEMO_SEEDS = 1_000_000;

export default async function handler(req: any, res: any) {
  // CORS preflight
  if (req.method === "OPTIONS") {
    res.writeHead(204, CORS_HEADERS);
    res.end();
    return;
  }

  Object.entries(CORS_HEADERS).forEach(([k, v]) => res.setHeader(k, v));

  if (req.method !== "POST") {
    res.statusCode = 405;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: "Method not allowed" }));
    return;
  }

  let body: any;
  try {
    body = typeof req.body === "string" ? JSON.parse(req.body) : req.body;
  } catch {
    res.statusCode = 400;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: "Invalid JSON in request body" }));
    return;
  }
  const jaml: string = body?.jaml;
  const seedCount: number = Math.min(
    Math.max(1, parseInt(body?.seed_count ?? "100000", 10)),
    MAX_DEMO_SEEDS
  );

  if (!jaml || typeof jaml !== "string") {
    res.statusCode = 400;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: "Missing required field: jaml (string)" }));
    return;
  }

  try {
    const result = await searchSeeds(jaml, seedCount);
    res.statusCode = 200;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify(result));
  } catch (err) {
    res.statusCode = 400;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: (err as Error).message }));
  }
}
