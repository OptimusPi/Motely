/**
 * POST /api/analyze
 *
 * Public demo endpoint — no auth required.
 * Body: { seed: string, jaml: string }
 * Returns: seed analysis JSON
 */
import dotnet, { MotelyWasmHost } from "motely-wasm";

await dotnet.boot();

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

export default async function handler(req: any, res: any) {
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

  const seed: string = body?.seed;
  const jaml: string = body?.jaml;

  if (!seed || typeof seed !== "string") {
    res.statusCode = 400;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: "Missing required field: seed (string)" }));
    return;
  }
  if (!jaml || typeof jaml !== "string") {
    res.statusCode = 400;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: "Missing required field: jaml (string)" }));
    return;
  }

  try {
    const configId = MotelyWasmHost.loadJaml(jaml);
    const deck = MotelyWasmHost.getConfigDeck(configId);
    const stake = MotelyWasmHost.getConfigStake(configId);
    const jsonStr = MotelyWasmHost.analyzeSeed(seed, deck, stake);
    res.statusCode = 200;
    res.setHeader("Content-Type", "application/json");
    res.end(jsonStr);
  } catch (err) {
    res.statusCode = 400;
    res.setHeader("Content-Type", "application/json");
    res.end(JSON.stringify({ error: (err as Error).message }));
  }
}
