import { NextResponse } from "next/server";
import type { SearchRequest, SearchResponse } from "@/lib/search-types";

export async function POST(request: Request) {
  const body = (await request.json().catch(() => null)) as Partial<SearchRequest> | null;

  if (!body || typeof body.jaml !== "string" || !body.jaml.trim()) {
    return NextResponse.json({ error: "JAML text is required." }, { status: 400 });
  }

  const requestId = crypto.randomUUID();
  const threads = Math.max(1, Math.trunc(body.threads ?? 1));
  const batchCharCount = Math.max(1, Math.trunc(body.batchCharCount ?? 1));

  // Simple placeholder - no external dependencies
  const response: SearchResponse = {
    ok: true,
    mode: "thin-client-placeholder",
    message: `Clean placeholder - no hacked dependencies. Request: ${requestId}, threads: ${threads}, batch: ${batchCharCount}`,
    shouldLabels: [],
    results: [],
    elapsedMs: 0,
  };

  return NextResponse.json(response);
}
