"use client";

import { useState, useCallback, useMemo } from "react";
import { useSearch } from "jaml-ui";
import { JamlIde } from "jaml-ui";
import { JimboBackground } from "jaml-ui/ui";
import { JimboBalatroFooter } from "jaml-ui/ui";
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
import type { JamlIdeSearchResult } from "jaml-ui";

/**
 * Seed Finder Page — App 3
 *
 * Requires a JAML filter to be loaded (from the IDE or direct input).
 * Runs the Motely WASM engine on your CPU to search 2.3 trillion seeds.
 *
 * Features:
 * - JAML input (inline editor or import from IDE)
 * - Search controls (mode, count)
 * - Real-time json-rendered results
 * - Export to analyzer
 */

const STARTER_JAML = `must:
  - joker: Blueprint
    antes: [1,2,3,4,5,6,7,8]
deck: Red
stake: White
`;

export function SeedFinderPage() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  const [showEditor, setShowEditor] = useState(true);
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

  const spec = useMemo<SpecType>(() => {
    if (search.status === "idle" && search.results.length === 0) {
      return buildLoadingSpec(
        "Load a JAML filter and click Search to find seeds. Use the IDE to write your filter first."
      );
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
      
        <main className="mx-auto flex w-full max-w-5xl flex-1 flex-col gap-8 px-4 py-8 md:py-12">
          <header className="flex flex-col gap-2">
            <h1 className="font-pixel text-3xl" style={{ color: "var(--j-blue)" }}>
              Seed Finder
            </h1>
            <p className="text-sm" style={{ color: "var(--j-grey)" }}>
              Load a JAML filter and search 2.3 trillion seeds. The Motely engine runs on your CPU.
            </p>
          </header>

          {/* JAML Section */}
          <section>
            <div className="mb-3 flex items-center justify-between">
              <div className="flex items-center gap-2">
                <span className="font-bold text-sm" style={{ color: "var(--j-white)" }}>
                  JAML Filter
                </span>
                <span className="text-xs" style={{ color: "var(--j-grey)" }}>
                  {showEditor ? "Editor open" : "Editor hidden"}
                </span>
              </div>
              <div className="flex gap-2">
                <button
                  className="rounded px-2 py-1 text-xs font-semibold"
                  style={{ border: "1px solid var(--j-panel-edge)", color: "var(--j-grey)" }}
                  onClick={() => setShowEditor(!showEditor)}
                >
                  {showEditor ? "Hide" : "Show"} Editor
                </button>
                <a
                  href="/ide"
                  className="rounded px-2 py-1 text-xs font-semibold"
                  style={{ backgroundColor: "var(--j-dark-blue)", color: "var(--j-blue)" }}
                >
                  Open IDE →
                </a>
              </div>
            </div>

            {showEditor && (
              <div className="rounded-lg border overflow-hidden" style={{ borderColor: "var(--j-panel-edge)" }}>
                <JamlIde
                  jaml={jaml}
                  onChange={setJaml}
                  title="Seed search"
                  subtitle={subtitle}
                />
              </div>
            )}
          </section>

          {/* Search Controls */}
          <section className="flex flex-wrap items-end gap-4">
            <div className="flex gap-2">
              <button
                className="rounded px-3 py-1.5 text-sm font-semibold"
                style={{
                  backgroundColor: searchMode === "random" ? "var(--j-blue)" : "var(--j-surface-inset)",
                  color: searchMode === "random" ? "#000" : "var(--j-white)",
                }}
                onClick={() => setSearchMode("random")}
              >
                Random
              </button>
              <button
                className="rounded px-3 py-1.5 text-sm font-semibold"
                style={{
                  backgroundColor: searchMode === "aesthetic" ? "var(--j-blue)" : "var(--j-surface-inset)",
                  color: searchMode === "aesthetic" ? "#000" : "var(--j-white)",
                }}
                onClick={() => setSearchMode("aesthetic")}
              >
                Aesthetic
              </button>
            </div>

            {searchMode === "random" && (
              <label className="flex items-center gap-2 text-sm" style={{ color: "var(--j-grey)" }}>
                Seeds:
                <input
                  type="number"
                  className="rounded border px-2 py-1 font-mono text-sm"
                  style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)", color: "var(--j-white)", width: 120 }}
                  value={seedCount}
                  min={10000}
                  max={100_000_000}
                  step={100_000}
                  onChange={(e) => setSeedCount(Math.max(10000, Number(e.target.value) || 1_000_000))}
                />
              </label>
            )}

            <button
              className="rounded px-5 py-2 text-sm font-semibold"
              style={{ backgroundColor: "var(--j-blue)", color: "#000" }}
              onClick={handleSearch}
            >
              {search.status === "running" ? "Stop" : "Search"}
            </button>
          </section>

          {/* Results */}
          <section>
            <div className="mb-3 flex items-center gap-2">
              <span className="font-bold text-sm" style={{ color: "var(--j-white)" }}>
                Results
              </span>
              <span className="text-xs" style={{ color: "var(--j-grey)" }}>
                via json-render
              </span>
            </div>
            <div
              className="rounded-lg border p-4"
              style={{ borderColor: "var(--j-panel-edge)", backgroundColor: "var(--j-dark-grey)" }}
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
                          window.open(`/analyzer?seed=${encodeURIComponent(args.seed)}`, "_blank");
                        }
                      },
                      copySeed: async (args) => {
                        if (args.seed) await navigator.clipboard.writeText(args.seed);
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
      <JimboBalatroFooter />
    </>
  );
}
