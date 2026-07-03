import { useCallback, useState } from "react";
import { JamlCodeEditor } from "jaml-codemirror";
import { JamlyzerView } from "jaml-ui";
import {
  MotelyJaml,
  MotelyJamlyzer,
  MotelySearch,
  type MotelyJamlyzerSeedResult,
} from "motely-wasm";

interface SearchResult {
  seed: string;
  score: number;
}

export function App({ initialJaml }: { initialJaml: string }) {
  const [jaml, setJaml] = useState(initialJaml);
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

  const runSearch = useCallback(
    async (mode: "list" | "random" | "sequential") => {
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
        if (mode === "list") {
          await MotelySearch.searchList(config);
        } else if (mode === "random") {
          await MotelySearch.searchRandom(config, 100_000);
        } else {
          await MotelySearch.searchSequential(config, 0n, 1n, 1);
        }
        setResults(found);
        setStatus("completed");
      } catch (err) {
        setStatus("error");
        setError(err instanceof Error ? err.message : String(err));
      } finally {
        MotelySearch.onScoredResult.unsubscribe(onResult);
        MotelySearch.onProgress.unsubscribe(onProgress);
      }
    },
    [jaml]
  );

  const analyzeSeed = useCallback((seed: string) => {
    try {
      const config = MotelyJaml.fromYaml(jaml);
      config.seeds = [seed];
      const [result] = MotelyJamlyzer.analyzeSeeds(config);
      setAnalysis({ seed, result, deck: config.deck, stake: config.stake });
    } catch (err) {
      setError(err instanceof Error ? err.message : String(err));
    }
  }, [jaml]);

  const loadJaml = useCallback(async () => {
    const [handle] = await (window as any).showOpenFilePicker({
      types: [{ description: "JAML files", accept: { "text/yaml": [".jaml", ".yaml", ".yml"] } }],
    });
    const file = await handle.getFile();
    const text = await file.text();
    setJaml(text);
  }, []);

  const saveJaml = useCallback(async () => {
    const handle = await (window as any).showSaveFilePicker({
      suggestedName: "filter.jaml",
      types: [{ description: "JAML file", accept: { "text/yaml": [".jaml"] } }],
    });
    const writable = await handle.createWritable();
    await writable.write(jaml);
    await writable.close();
  }, [jaml]);


  return (
    <div
      style={{
        minHeight: "100vh",
        background: "#1a1b26",
        color: "#c0caf5",
        padding: 16,
        fontFamily: "system-ui, sans-serif",
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
        <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
          <h1 style={{ margin: 0, fontSize: 20 }}>Seed Finder</h1>
          <div style={{ display: "flex", gap: 8 }}>
            <button onClick={loadJaml}>Load</button>
            <button onClick={saveJaml}>Save</button>
          </div>
          <div
            style={{
              flex: 1,
              minHeight: 320,
              border: "1px solid #414868",
              borderRadius: 8,
              overflow: "hidden",
            }}
          >
            <JamlCodeEditor
              value={jaml}
              onChange={setJaml}
              height="100%"
              placeholder="Write your JAML filter here..."
            />
          </div>
          <div style={{ display: "flex", gap: 8, flexWrap: "wrap" }}>
            <button onClick={() => runSearch("list")} disabled={status === "running"}>
              Search List
            </button>
            <button onClick={() => runSearch("random")} disabled={status === "running"}>
              Search Random
            </button>
            <button onClick={() => runSearch("sequential")} disabled={status === "running"}>
              Search Sequential
            </button>
          </div>
          {error && (
            <div style={{ color: "#f7768e", fontSize: 13 }}>{error}</div>
          )}
          <div
            style={{
              background: "#24283b",
              border: "1px solid #414868",
              borderRadius: 8,
              padding: 12,
            }}
          >
            <div
              style={{
                display: "flex",
                justifyContent: "space-between",
                alignItems: "center",
                marginBottom: 12,
              }}
            >
              <span style={{ fontSize: 13, color: "#7aa2f7" }}>
                {status === "running" ? "Searching..." : `${results.length} results`}
              </span>
              <span style={{ fontSize: 13, color: "#565f89" }}>
                {progress.seedsSearched} searched
              </span>
            </div>
            <div style={{ display: "flex", flexDirection: "column", gap: 8 }}>
              {results.map((r) => (
                <button
                  key={r.seed}
                  onClick={() => analyzeSeed(r.seed)}
                  disabled={status === "running"}
                  style={{
                    textAlign: "left",
                    background: "#1a1b26",
                    border: "1px solid #414868",
                    borderRadius: 6,
                    padding: "8px 12px",
                    color: "#c0caf5",
                    cursor: "pointer",
                  }}
                >
                  <div style={{ fontWeight: 600 }}>{r.seed}</div>
                  <div style={{ fontSize: 12, color: "#565f89" }}>Score: {r.score}</div>
                </button>
              ))}
            </div>
          </div>
        </div>

        <div>
          {analysis ? (
            <JamlyzerView
              result={analysis.result}
              deck={analysis.deck}
              stake={analysis.stake}
            />
          ) : (
            <div
              style={{
                padding: 24,
                background: "#24283b",
                borderRadius: 8,
                color: "#565f89",
              }}
            >
              Click a seed result to run Jamlyzer and display the full ante-by-ante breakdown.
            </div>
          )}
        </div>
      </div>
    </div>
  );
}
