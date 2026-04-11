/**
 * POST /api/analyze
 *
 * Public demo endpoint — no auth required.
 * Body: { seed: string, jaml: string }
 * Returns: MotelySingleSearchContext JSON
 */
import { analyzeSeed } from "./tools.js";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "POST, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
};

export default async function handler(req: any, res: any) {
  // CORS preflight
  if (req.method === "OPTIONS") {
    res.writeHead(204, CORS_HEADERS);
    res.end();
    return;
  }

  Object.entries(CORS_HEADERS).forEach(([k, v]) => res.setHeader(k, v));

  if (req.method !== "POST") {
    res.status(405).json({ error: "Method not allowed" });
    return;
  }

  let body: any;
  try {
    body = typeof req.body === "string" ? JSON.parse(req.body) : req.body;
  } catch {
    res.status(400).json({ error: "Invalid JSON in request body" });
    return;
  }
  const seed: string = body?.seed;
  const jaml: string = body?.jaml;

  if (!seed || typeof seed !== "string") {
    res.status(400).json({ error: "Missing required field: seed (string)" });
    return;
  }
  if (!jaml || typeof jaml !== "string") {
    res.status(400).json({ error: "Missing required field: jaml (string)" });
    return;
  }

  try {
    const result = await analyzeSeed(seed, jaml);
    res.status(200).json(result);
  } catch (err) {
    res.status(400).json({ error: (err as Error).message });
  }
}
