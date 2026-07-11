import { useCallback, useState } from "react";
import { JamlCodeEditor } from "jaml-codemirror";
import { JamlyzerView, JimboButton, JimboPanel } from "jaml-ui";
import {
  MotelyJaml,
  MotelyJamlyzer,
  MotelySearch,
  type MotelyJamlyzerSeedResult,
} from "motely-wasm";
import { STARTER_JAML } from "./constants.js";

interface SearchResult {
  seed: string;
  score: number;
}

export function App() {
  const [jaml, setJaml] = useState(STARTER_JAML);
  const [status, setStatus] = useState<"idle" | "running" | "completed" | "error">("idle");
  const [error, setError] = useState<string | null>(null);
  const [results, setResults] = useState<SearchResult[]>([]);
  const [progress, setProgress] = useState({
    seedsSearched: "0",
    matchingSeeds: "0",
    seedsPerSecond: "0",
  });
  const [analysis, setAnalysis] = useState<{
    seed: string;
    result: MotelyJamlyzerSeedResult;
    deck: ReturnType<typeof MotelyJaml.fromYaml>["deck"];
    stake: ReturnType<typeof MotelyJaml.fromYaml>["stake"];
  } | null>(null);

  const runSearch = useCallback(async () => {
    setStatus("running");
    setError(null);
    setResults([]);
    setAnalysis(null);

    let config: ReturnType<typeof MotelyJaml.fromYaml>;
    try {
      config = MotelyJaml.fromYaml(jaml);
    } catch (err) {
      setStatus("error");
      setError(err instanceof Error ? err.message : String(err));
      return;
    }

    const found: SearchResult[] = [];
    const onResult = (r: { seed: string; score: number }) => found.push(r);
    const onProgress = (p: {
      seedsSearched: bigint;
      matchingSeeds: bigint;
      seedsPerMillisecond: number;
    }) =>
      setProgress({
        seedsSearched: p.seedsSearched.toLocaleString(),
        matchingSeeds: p.matchingSeeds.toLocaleString(),
        seedsPerSecond: (p.seedsPerMillisecond * 1000).toLocaleString(),
      });

    MotelySearch.onScoredResult.subscribe(onResult);
    MotelySearch.onProgress.subscribe(onProgress);

    try {
      await MotelySearch.searchRandom(config, 100_000);
      setResults(found);
      setStatus("completed");
    } catch (err) {
      setStatus("error");
      setError(err instanceof Error ? err.message : String(err));
    } finally {
      MotelySearch.onScoredResult.unsubscribe(onResult);
      MotelySearch.onProgress.unsubscribe(onProgress);
    }
  }, [jaml]);

  const analyzeSeed = useCallback(
    (seed: string) => {
      try {
        const config = MotelyJaml.fromYaml(jaml);
        config.seeds = [seed];
        const [result] = MotelyJamlyzer.analyzeSeeds(config);
        setAnalysis({ seed, result, deck: config.deck, stake: config.stake });
      } catch (err) {
        setError(err instanceof Error ? err.message : String(err));
      }
    },
    [jaml]
  );

  return (
    <div
      style={{
        minHeight: "100vh",
        background: "var(--j-darkest)",
        color: "var(--j-text-color, #fff)",
        padding: 16,
        fontFamily: "inherit",
      }}
    >
      <div
        style={{
          maxWidth: 960,
          margin: "0 auto",
          display: "grid",
          gridTemplateColumns: "320px 1fr",
          gap: 16,
        }}
      >
        <JimboPanel style={{ gap: 12 }}>
          <h1 className="j-text j-text--label j-text--upper" style={{ margin: 0, fontSize: 20 }}>
            Seed Finder
          </h1>
          <div style={{ flex: 1, minHeight: 320, borderRadius: 8, overflow: "hidden" }}>
            <JamlCodeEditor
              value={jaml}
              onChange={setJaml}
              height="100%"
              placeholder="Write your JAML filter here..."
            />
          </div>
          <JimboButton tone="orange" onClick={runSearch} disabled={status === "running"}>
            {status === "running" ? "Searching..." : "Search Random"}
          </JimboButton>
          {error && (
            <div className="j-text j-text--micro j-text--red">{error}</div>
          )}
          <JimboPanel style={{ padding: 12 }}>
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                marginBottom: 12,
              }}
            >
              <span className="j-text j-text--micro j-text--blue">
                {status === "running" ? "Searching..." : `${results.length} results`}
              </span>
              <span className="j-text j-text--micro j-text--grey">
                {progress.seedsSearched} searched
              </span>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {results.map((r) => (
                <JimboButton
                  key={r.seed}
                  size="sm"
                  tone="blue"
                  onClick={() => analyzeSeed(r.seed)}
                  disabled={status === "running"}
                >
                  {r.seed} — {r.score}
                </JimboButton>
              ))}
            </div>
          </JimboPanel>
        </JimboPanel>

        <div>
          {analysis ? (
            <JamlyzerView result={analysis.result} deck={analysis.deck} stake={analysis.stake} />
          ) : (
            <JimboPanel>
              <span className="j-text j-text--micro j-text--grey">
                Click a seed result to run Jamlyzer and display the full ante-by-ante breakdown.
              </span>
            </JimboPanel>
          )}
        </div>
      </div>
    </div>
  );
}
