import { SeedTable } from "@/components/seed-table"

export default function Home() {
  return (
    <main className="min-h-screen bg-background">
      <div className="mx-auto max-w-7xl px-4 py-8">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold tracking-tight text-foreground">
            Balatro Seed Browser
          </h1>
          <p className="mt-2 text-muted-foreground">
            Search and browse billions of Balatro seeds with server-side pagination.
          </p>
        </div>

        {/* Seed table */}
        <SeedTable />

        {/* Footer info */}
        <div className="mt-8 rounded-lg border bg-muted/30 p-4 text-sm text-muted-foreground">
          <p className="font-medium text-foreground mb-2">Configuration</p>
          <ul className="space-y-1 font-mono text-xs">
            <li><code>DUCKDB_PATH</code> - Path to your .duckdb file</li>
            <li><code>DUCKDB_TABLE</code> - Table name (default: seeds)</li>
            <li><code>DUCKDB_SEED_COLUMN</code> - Primary seed column (default: seed)</li>
          </ul>
        </div>
      </div>
    </main>
  )
}
