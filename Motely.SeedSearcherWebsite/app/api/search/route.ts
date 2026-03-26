import { NextResponse } from "next/server";
import type { SearchRequest, SearchResponse } from "@/lib/search-types";

export async function POST(request: Request) {
  const body = (await request.json().catch(() => null)) as Partial<SearchRequest> | null;

  if (!body || typeof body.jaml !== "string" || !body.jaml.trim()) {
    return NextResponse.json({ error: "JAML text is required." }, { status: 400 });
  }

  const response: SearchResponse = {
    ok: true,
    mode: "thin-client-placeholder",
    message:
      "Thin-client placeholder route. Replace this handler with the real Node/C# worker contract once the search backend endpoint is ready.",
    shouldLabels: [],
    results: [],
    elapsedMs: 0,
  };

  return NextResponse.json(response);
}
