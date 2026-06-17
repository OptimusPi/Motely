"use client";

import { useState, useCallback, useMemo } from "react";
import { useSearch, JamlIde, type JamlIdeSearchResult } from "jaml-ui";
import { JimboApp, JimboBackground } from "jaml-ui/ui";
import {
  Renderer,
  StateProvider,
  ActionProvider,
  VisibilityProvider,
  ValidationProvider,
} from "@json-render/react";
import { registry } from "@/lib/registry";
import { buildSearchSpec, buildLoadingSpec } from "@/lib/spec-builder";
import type { SpecType } from "@json-render/react";

const STARTER_JAML = `must:
  - joker: Blueprint
    antes: [1,2,3,4,5,6,7,8]
deck: Red
stake: White
`;

export default function FindPage() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  const [searchMode, setSearchMode] = useState<"random" | "aesthetic">("random");
  const [seedCount, setSeedCount] = useState(1_000_000);
  const search = useSearch();

  const searchResults = useMemo<JamlIdeSearchResult[]>(
    () =>
      search.results.map((result) => ({
        seed: result.seed,
        score: result.score,
        tallyColumns: result.tallyColumns,
        tallyLabels: search.tallyLabels,
      })),
    [search.results, search.tallyLabels],
  );

  const handleSearch = useCallback(() => {
    if (search.status === "running") {
      search.cancel();
      return;
    }
    if (searchMode === "random") {
      search.startRandom(jaml, seedCount);
    } else {
      search.startAesthetic(jaml, 0);
    }
  }, [search, jaml, searchMode, seedCount]);

  // Build json-render spec from search state
  const spec = useMemo<SpecType>(() => {
    if (search.status === "idle" && search.results.length === 0) {
      return buildLoadingSpec("Enter a JAML filter and click Search to find seeds.");
    }
    return buildSearchSpec({
      status: search.status,
      seedsSearched: search.totalSearched.toString(),
      matchesFound: Number(search.matchingSeeds),
      seedsPerSecond: search.seedsPerSecond,
      error: search.error,
      results: search.results.map((r) => ({
        seed: r.seed,
        score: r.score,
        highlights: r.tallyColumns
          ?.map((v, i) => (v > 0 ? search.tallyLabels[i] : null))
          .filter(Boolean) as string[] | undefined,
      })),
    });
  }, [search]);

  const subtitle =
    search.status === "running"
      ? `Searching ${search.totalSearched.toString()} seeds at ${Math.round(search.seedsPerSecond)}/s`
      : search.status === "completed"
        ? `Done. ${search.matchingSeeds.toString()} matches.`
        : search.status === "error"
          ? `Error: ${search.error}`
          : "Ready to search.";

  return (
    <>
      <JimboBackground />
      <JimboApp>
        <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-8 md:py-12">
          {/* Header */}
          <header className="flex flex-col gap-2">
            <h1 className="font-pixel text-3xl" style={{ color: "var(--j-accent)" }}>
              Find Seeds
            </h1>
            <p className="text-sm" style={{ color: "var(--j-muted)" }}>
              Write a JAML filter and search 2.3 trillion seeds. The Motely engine runs on your CPU.
            </p>
          </header>

          {/* Search Mode + Controls */}
          <div className="flex flex-wrap items-center gap-4">
            <div className="flex gap-2">
              <button
                className="rounded px-3 py-1.5 text-sm font-semibold"
                style={{
                  backgroundColor: searchMode === "random" ? "var(--j-accent)" : "var(--j-surface-muted)",
                  color: searchMode === "random" ? "#000" : "var(--j-foreground)",
                }}
                onClick={() => setSearchMode("random")}
              >
                Random
              </button>
              <button
                className="rounded px-3 py-1.5 text-sm font-semibold"
                style={{
                  backgroundColor: searchMode === "aesthetic" ? "var(--j-accent)" : "var(--j-surface-muted)",
                  color: searchMode === "aesthetic" ? "#000" : "var(--j-foreground)",
                }}
                onClick={() => setSearchMode("aesthetic")}
              >
                Aesthetic
              </button>
            </div>

            {searchMode === "random" && (
              <label className="flex items-center gap-2 text-sm" style={{ color: "var(--j-muted)" }}>
                Seeds:
                <input
                  type="number"
                  className="rounded border px-2 py-1 font-mono text-sm"
                  style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)", color: "var(--j-foreground)", width: 120 }}
                  value={seedCount}
                  min={10000}
                  max={100_000_000}
                  step={100_000}
                  onChange={(e) => setSeedCount(Math.max(10000, Number(e.target.value) || 1_000_000))}
                />
              </label>
            )}
          </div>

          {/* JAML Editor */}
          <JamlIde
            jaml={jaml}
            onChange={setJaml}
            searchResults={searchResults}
            isSearching={search.status === "running"}
            onSearch={handleSearch}
            title="Seed search"
            subtitle={subtitle}
          />

          {/* json-render Results */}
          <section>
            <div className="mb-3 flex items-center gap-2">
              <span className="font-bold text-sm" style={{ color: "var(--j-foreground)" }}>
                AI-Rendered Results
              </span>
              <span className="text-xs" style={{ color: "var(--j-muted)" }}>
                via json-render
              </span>
            </div>
            <div
              className="rounded-lg border p-4"
              style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
            >
              <StateProvider initialState={{}}>
                <VisibilityProvider>
                  <ActionProvider
                    handlers={{
                      searchSeeds: async (args) => {
                        if (args.jaml) setJaml(args.jaml);
                        handleSearch();
                      },
                      analyzeSeed: async (args) => {
                        if (args.seed) {
                          window.open(`/analyze?seed=${encodeURIComponent(args.seed)}`, "_blank");
                        }
                      },
                      copySeed: async (args) => {
                        if (args.seed) {
                          await navigator.clipboard.writeText(args.seed);
                        }
                      },
                      cancelSearch: async () => search.cancel(),
                      rerunSearch: async () => handleSearch(),
                    }}
                  >
                    <ValidationProvider>
                      <Renderer spec={spec} registry={registry} />
                    </ValidationProvider>
                  </ActionProvider>
                </VisibilityProvider>
              </StateProvider>
            </div>
          </section>
        </main>
      </JimboApp>
    </>
  );
}
