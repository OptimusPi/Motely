import { defineRegistry } from "@json-render/react";
import type { SearchResponse, SeedAnalysis, AnteAnalysis } from "./searchTypes.js";
import { jamlSearchCatalog } from "./catalog.js";

// ── Ante detail sub-component (not in json-render, just React) ──────────────

function AnteCard({ ante }: { ante: AnteAnalysis }) {
  return (
    <div
      style={{
        padding: "10px 12px",
        borderRadius: 8,
        background: "var(--bg2, #1a2332)",
        border: "1px solid var(--border, #334461)",
        marginBottom: 8,
      }}
    >
      <div
        style={{
          fontWeight: 700,
          color: "var(--gold, #ffc640)",
          fontSize: "0.9rem",
          marginBottom: 6,
        }}
      >
        Ante {ante.ante}
      </div>
      <div style={{ fontSize: "0.82rem", lineHeight: 1.8 }}>
        <div>
          <span style={{ color: "var(--text3, #708386)" }}>Boss: </span>
          <span style={{ color: "var(--red, #ff4c40)", fontWeight: 600 }}>
            {ante.boss}
          </span>
        </div>
        <div>
          <span style={{ color: "var(--text3, #708386)" }}>Voucher: </span>
          <span style={{ color: "var(--blue, #0093ff)" }}>{ante.voucher}</span>
        </div>
        <div>
          <span style={{ color: "var(--text3, #708386)" }}>Tags: </span>
          <span>{ante.smallBlindTag}, {ante.bigBlindTag}</span>
        </div>
        {ante.drawOrder && (
          <div>
            <span style={{ color: "var(--text3, #708386)" }}>Draw: </span>
            <span style={{ fontFamily: "ui-monospace, monospace", fontSize: "0.78rem" }}>
              {ante.drawOrder}
            </span>
          </div>
        )}
        {ante.shopQueue.length > 0 && (
          <div style={{ marginTop: 4 }}>
            <div style={{ color: "var(--text3, #708386)", marginBottom: 2 }}>Shop:</div>
            <div
              style={{
                paddingLeft: 8,
                fontFamily: "ui-monospace, monospace",
                fontSize: "0.78rem",
                color: "var(--text2, #a0a8b8)",
              }}
            >
              {ante.shopQueue.map((item, i) => (
                <div key={i}>
                  {i + 1}) {item}
                </div>
              ))}
            </div>
          </div>
        )}
        {ante.packs.length > 0 && (
          <div style={{ marginTop: 4 }}>
            <div style={{ color: "var(--text3, #708386)", marginBottom: 2 }}>Packs:</div>
            <div
              style={{
                paddingLeft: 8,
                fontSize: "0.78rem",
                color: "var(--text2, #a0a8b8)",
              }}
            >
              {ante.packs.map((p, i) => (
                <div key={i}>
                  <span style={{ color: "var(--green-text, #35bd86)" }}>{p.type}</span>
                  {p.items.length > 0 && (
                    <span> — {p.items.join(", ")}</span>
                  )}
                </div>
              ))}
            </div>
          </div>
        )}
      </div>
    </div>
  );
}

// ── json-render component registry ──────────────────────────────────────────

export const { registry } = defineRegistry(jamlSearchCatalog, {
  components: {
    Stack: ({ props, children }) => (
      <div style={{ display: "flex", flexDirection: "column", gap: 12 }}>
        <h2
          style={{
            margin: 0,
            fontSize: "1.1rem",
            color: "var(--gold, #ffc640)",
            fontWeight: 700,
          }}
        >
          {props.heading}
        </h2>
        {children}
      </div>
    ),
    FilterDisplay: ({ props }) => {
      const text = props.jummy || props.jaml;
      if (!text) return null;
      return (
        <div
          style={{
            padding: "8px 12px",
            borderRadius: 8,
            background: "var(--bg3, #2a3233)",
            border: "1px solid var(--border2, #4f6367)",
            fontSize: "0.8rem",
            fontFamily: "ui-monospace, monospace",
            color: "var(--text2, #a0a8b8)",
            whiteSpace: "pre-wrap",
            wordBreak: "break-word",
          }}
        >
          <span style={{ color: "var(--text3, #708386)", fontSize: "0.72rem", textTransform: "uppercase", letterSpacing: "0.05em" }}>
            {props.jummy ? "Jummy" : "JAML"}
          </span>
          <div style={{ marginTop: 4 }}>{text}</div>
        </div>
      );
    },
    StatsBlock: ({ props }) => (
      <div
        style={{
          padding: "10px 14px",
          borderRadius: 8,
          background: "var(--panel, #374244)",
          border: "1px solid var(--border, #334461)",
          fontSize: "0.85rem",
          lineHeight: 1.7,
          display: "flex",
          gap: 20,
          flexWrap: "wrap",
        }}
      >
        <div>
          <span style={{ color: "var(--text2, #a0a8b8)" }}>Status </span>
          <span style={{ color: "var(--green-text, #35bd86)", fontWeight: 600 }}>
            {props.status}
          </span>
        </div>
        <div>
          <span style={{ color: "var(--text2, #a0a8b8)" }}>Searched </span>
          <strong>{Number(props.seedsSearched).toLocaleString()}</strong>
        </div>
        <div>
          <span style={{ color: "var(--text2, #a0a8b8)" }}>Matches </span>
          <strong style={{ color: "var(--blue, #0093ff)" }}>
            {props.matchesFound}
          </strong>
          {props.resultsShown &&
            props.resultsShown !== props.matchesFound && (
              <span style={{ color: "var(--text3, #708386)", fontSize: "0.8rem" }}>
                {" "}(top {props.resultsShown})
              </span>
            )}
        </div>
      </div>
    ),
    SeedTable: ({ props, emit }) => {
      const rows = (props.rows ?? []) as Array<{
        seed: string;
        score: string;
        tally?: number[];
      }>;
      const hasTally = rows.some((r) => r.tally && r.tally.length > 0);
      return (
        <div
          style={{
            borderRadius: 8,
            border: "1px solid var(--border, #334461)",
            overflow: "hidden",
            background: "var(--bg2, #1a2332)",
          }}
        >
          <table className="seed-table">
            <thead>
              <tr>
                <th style={{ width: "1%" }}>#</th>
                <th>Seed</th>
                <th style={{ textAlign: "right" }}>Score</th>
                {hasTally && <th>Tally</th>}
                <th style={{ width: "1%" }}></th>
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr
                  key={i}
                  style={{ cursor: "pointer" }}
                  onClick={() => emit("press", { seed: r.seed })}
                >
                  <td style={{ color: "var(--text3, #708386)", fontFamily: "ui-monospace, monospace", fontSize: "0.75rem" }}>
                    {i + 1}
                  </td>
                  <td
                    style={{
                      fontFamily: "ui-monospace, monospace",
                      fontWeight: 600,
                      color: "var(--gold-text, #e4b643)",
                    }}
                  >
                    {r.seed}
                  </td>
                  <td
                    style={{
                      fontFamily: "ui-monospace, monospace",
                      textAlign: "right",
                      color: "var(--blue, #0093ff)",
                      fontWeight: 500,
                    }}
                  >
                    {r.score}
                  </td>
                  {hasTally && (
                    <td
                      style={{
                        fontFamily: "ui-monospace, monospace",
                        color: "var(--text2, #a0a8b8)",
                        fontSize: "0.78rem",
                      }}
                    >
                      {r.tally && r.tally.length > 0
                        ? r.tally.join(" / ")
                        : "\u2014"}
                    </td>
                  )}
                  <td
                    style={{
                      color: "var(--accent, #935adc)",
                      fontSize: "0.75rem",
                      whiteSpace: "nowrap",
                    }}
                  >
                    Analyze
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      );
    },
    SeedDetail: ({ props, emit }) => {
      const loading = props.loading === "true";
      const error = props.error;
      let analysis: SeedAnalysis | null = null;
      if (props.analysisJson) {
        try {
          analysis = JSON.parse(props.analysisJson) as SeedAnalysis;
        } catch {}
      }

      return (
        <div
          style={{
            borderRadius: 10,
            border: "2px solid var(--accent, #935adc)",
            background: "var(--bg, #1e2b2d)",
            padding: 16,
          }}
        >
          <div style={{ display: "flex", justifyContent: "space-between", alignItems: "center", marginBottom: 12 }}>
            <h3
              style={{
                margin: 0,
                fontFamily: "ui-monospace, monospace",
                color: "var(--gold, #ffc640)",
                fontSize: "1rem",
              }}
            >
              Seed: {props.seed}
            </h3>
            <button
              type="button"
              onClick={() => emit("press")}
              style={{
                background: "var(--grey-dark, #3a5055)",
                border: "none",
                color: "var(--text, #f6f0d5)",
                padding: "4px 12px",
                borderRadius: 6,
                cursor: "pointer",
                fontSize: "0.8rem",
              }}
            >
              Close
            </button>
          </div>
          {loading && (
            <div style={{ display: "flex", alignItems: "center", gap: 8, color: "var(--text2, #a0a8b8)", fontSize: "0.85rem" }}>
              <span className="spinner" />
              Analyzing seed...
            </div>
          )}
          {error && (
            <div style={{ color: "var(--red, #ff4c40)", fontSize: "0.85rem" }}>
              {error}
            </div>
          )}
          {analysis?.error && (
            <div style={{ color: "var(--red, #ff4c40)", fontSize: "0.85rem" }}>
              {analysis.error}
            </div>
          )}
          {analysis?.antes && analysis.antes.length > 0 && (
            <div style={{ display: "flex", flexDirection: "column", gap: 0 }}>
              {analysis.antes.map((a) => (
                <AnteCard key={a.ante} ante={a} />
              ))}
            </div>
          )}
        </div>
      );
    },
    Button: ({ props, emit }) => {
      const disabled = props.disabled === "true";
      return (
        <button
          type="button"
          disabled={disabled}
          onClick={() => emit("press")}
          style={{
            marginTop: 4,
            padding: "8px 16px",
            borderRadius: 8,
            border: "none",
            background: disabled
              ? "var(--grey-dark, #3a5055)"
              : "var(--red, #ff4c40)",
            color: disabled ? "var(--text3, #708386)" : "var(--text, #f6f0d5)",
            fontWeight: 600,
            fontSize: "0.85rem",
            cursor: disabled ? "not-allowed" : "pointer",
            opacity: disabled ? 0.6 : 1,
          }}
        >
          {props.label}
        </button>
      );
    },
    Text: ({ props }) => (
      <pre
        style={{
          whiteSpace: "pre-wrap",
          wordBreak: "break-word",
          fontSize: "0.82rem",
          color:
            props.variant === "error"
              ? "var(--red, #ff4c40)"
              : "var(--text, #f6f0d5)",
          margin: 0,
          padding: 8,
          borderRadius: 6,
          background:
            props.variant === "error"
              ? "var(--red-dark, #a02721)"
              : "transparent",
        }}
      >
        {props.body}
      </pre>
    ),
    EmptyState: ({ props }) => (
      <div
        style={{
          padding: 24,
          textAlign: "center",
          color: "var(--text2, #a0a8b8)",
          fontSize: "0.9rem",
          background: "var(--bg2, #1a2332)",
          borderRadius: 8,
          border: "1px solid var(--border, #334461)",
        }}
      >
        {props.message}
      </div>
    ),
    Spinner: ({ props }) => (
      <div style={{ display: "flex", alignItems: "center", gap: 10, padding: "12px 0" }}>
        <div
          style={{
            width: 20,
            height: 20,
            border: "3px solid var(--border, #334461)",
            borderTopColor: "var(--blue, #0093ff)",
            borderRadius: "50%",
            animation: "spin 0.8s linear infinite",
          }}
        />
        <span style={{ color: "var(--text2, #a0a8b8)", fontSize: "0.85rem" }}>
          {props.message || "Searching..."}
        </span>
      </div>
    ),
  },
});

const MAX_UI_ROWS = 200;

export function buildSpecFromSearch(
  output: SearchResponse,
  filterInfo?: { jummy?: string; jaml?: string }
) {
  const elements: Record<string, Record<string, unknown>> = {};
  const childKeys: string[] = [];
  const slice = output.results.slice(0, MAX_UI_ROWS);

  // Filter display
  if (filterInfo && (filterInfo.jummy || filterInfo.jaml)) {
    elements["filter"] = {
      type: "FilterDisplay",
      props: {
        jummy: filterInfo.jummy ?? undefined,
        jaml: filterInfo.jaml ?? undefined,
      },
      children: [],
    };
    childKeys.push("filter");
  }

  elements["stats"] = {
    type: "StatsBlock",
    props: {
      status: output.status,
      seedsSearched: output.seedsSearched,
      matchesFound: output.matchesFound,
      resultsShown: output.resultsShown,
    },
    children: [],
  };
  childKeys.push("stats");

  if (slice.length === 0) {
    elements["empty"] = {
      type: "EmptyState",
      props: {
        message: "No matching seeds found. Try broadening your filter.",
      },
      children: [],
    };
    childKeys.push("empty");
  } else {
    const rows = slice.map((r) => ({
      seed: r.seed,
      score: String(r.score),
      tally: Array.isArray(r.tally) ? r.tally : Array.from(r.tally),
    }));
    elements["table"] = {
      type: "SeedTable",
      props: { rows },
      on: {
        press: { action: "analyzeSeed", params: {} },
      },
      children: [],
    };
    childKeys.push("table");
  }

  elements["again"] = {
    type: "Button",
    props: { label: "Re-roll (same filter)", disabled: "false" },
    on: {
      press: { action: "rerunSearch", params: {} },
    },
    children: [],
  };
  childKeys.push("again");

  elements["root"] = {
    type: "Stack",
    props: { heading: "Balatro Seed Search" },
    children: childKeys,
  };

  return {
    root: "root",
    elements,
  };
}

export function buildSeedDetailSpec(
  seed: string,
  state: { loading?: boolean; error?: string; analysisJson?: string }
) {
  return {
    root: "root",
    elements: {
      root: {
        type: "Stack",
        props: { heading: `Seed Analysis` },
        children: ["detail"],
      },
      detail: {
        type: "SeedDetail",
        props: {
          seed,
          loading: state.loading ? "true" : "false",
          error: state.error ?? undefined,
          analysisJson: state.analysisJson ?? undefined,
        },
        on: {
          press: { action: "closeSeedDetail", params: {} },
        },
        children: [],
      },
    },
  };
}

export function buildErrorSpec(message: string) {
  return {
    root: "root",
    elements: {
      root: {
        type: "Stack",
        props: { heading: "Search" },
        children: ["err"],
      },
      err: {
        type: "Text",
        props: { body: message, variant: "error" },
        children: [],
      },
    },
  };
}

export function buildLoadingSpec(message?: string) {
  return {
    root: "root",
    elements: {
      root: {
        type: "Stack",
        props: { heading: "Balatro Seed Search" },
        children: ["spinner"],
      },
      spinner: {
        type: "Spinner",
        props: { message: message ?? "Searching..." },
        children: [],
      },
    },
  };
}
