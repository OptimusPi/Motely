/**
 * POST /api/analyze
 * Returns a seed analysis plan.
 * The actual analysis runs client-side via motely-wasm.
 */
export async function POST(request: Request) {
  try {
    const body = await request.json();
    const seed = body?.seed;
    const deck = body?.deck ?? "Red";
    const stake = body?.stake ?? "White";

    if (!seed || typeof seed !== "string") {
      return Response.json({ error: "Missing seed" }, { status: 400 });
    }

    return Response.json({
      status: "planned",
      seed,
      deck,
      stake,
      message: "Analysis planned. Run client-side via motely-wasm for best performance.",
    });
  } catch (err) {
    return Response.json(
      { error: (err as Error).message },
      { status: 500 }
    );
  }
}
