/**
 * POST /api/search
 * Simple proxy that validates input and returns a plan.
 * The actual search runs client-side via motely-wasm.
 */
export async function POST(request: Request) {
  try {
    const body = await request.json();
    const jaml = body?.jaml;
    const seedCount = Math.min(Math.max(1, parseInt(body?.seed_count ?? "100000", 10)), 1_000_000);

    if (!jaml || typeof jaml !== "string") {
      return Response.json({ error: "Missing jaml" }, { status: 400 });
    }

    // Return a search plan — the client will execute the actual search
    return Response.json({
      status: "planned",
      jaml,
      seedCount,
      message: "Search planned. Run client-side via motely-wasm for best performance.",
    });
  } catch (err) {
    return Response.json(
      { error: (err as Error).message },
      { status: 500 }
    );
  }
}
