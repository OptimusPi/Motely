import { NextRequest, NextResponse } from "next/server"
import { isDuckDBAvailable, query, querySingle } from "@/lib/duckdb"

export async function GET(request: NextRequest) {
  if (!isDuckDBAvailable()) {
    return NextResponse.json(
      {
        error: "Seeds API unavailable",
        details: "DuckDB is not available on this deployment (e.g. Vercel). Use self-hosted backend or the WASM client for seed search.",
      },
      { status: 503 }
    )
  }

  const searchParams = request.nextUrl.searchParams
  const page = parseInt(searchParams.get("page") || "1", 10)
  const limit = Math.min(parseInt(searchParams.get("limit") || "100", 10), 500)
  const search = searchParams.get("search") || ""
  const offset = (page - 1) * limit

  try {
    // Build query with optional search
    // Adjust the table name and column names to match your schema
    const tableName = process.env.DUCKDB_TABLE || "seeds"
    const seedColumn = process.env.DUCKDB_SEED_COLUMN || "seed"

    let whereClause = ""
    const params: unknown[] = []

    if (search) {
      whereClause = `WHERE ${seedColumn} ILIKE ?`
      params.push(`%${search}%`)
    }

    // Get total count
    const countResult = await querySingle<{ count: number }>(
      `SELECT COUNT(*) as count FROM ${tableName} ${whereClause}`,
      params
    )
    const totalCount = Number(countResult?.count || 0)

    // Get paginated results
    const seeds = await query(
      `SELECT * FROM ${tableName} ${whereClause} ORDER BY ${seedColumn} LIMIT ? OFFSET ?`,
      [...params, limit, offset]
    )

    return NextResponse.json({
      seeds,
      pagination: {
        page,
        limit,
        totalCount,
        totalPages: Math.ceil(totalCount / limit),
        hasNext: page * limit < totalCount,
        hasPrev: page > 1,
      },
    })
  } catch (error) {
    console.error("DuckDB query error:", error)
    return NextResponse.json(
      { error: "Failed to query seeds", details: String(error) },
      { status: 500 }
    )
  }
}
