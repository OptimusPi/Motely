import { defineRegistry } from "@json-render/react";
import type { SearchResponse } from "./searchTypes.js";
import { jamlSearchCatalog } from "./catalog.js";

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
    SeedTable: ({ props }) => {
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
              </tr>
            </thead>
            <tbody>
              {rows.map((r, i) => (
                <tr key={i}>
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
                </tr>
              ))}
            </tbody>
          </table>
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
  },
});

const MAX_UI_ROWS = 200;

export function buildSpecFromSearch(output: SearchResponse) {
  const elements: Record<string, Record<string, unknown>> = {};
  const childKeys: string[] = ["stats"];
  const slice = output.results.slice(0, MAX_UI_ROWS);

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
