import { defineRegistry } from "@json-render/react";
import { balatroCatalog } from "./catalog.js";
import {
  JamlGameCard,
  JamlIde,
  JimboApp,
  JimboBackground,
  JimboPanel,
  JimboButton,
  JamlMapPreview,
  useSearch,
  useAnalyzer,
} from "jaml-ui";
import React from "react";

// ── Helper: jaml-ui card type mapping ──
function mapToGameCardType(
  type: "joker" | "tarot" | "planet" | "spectral" | "playing" | "pack" | "voucher" | "tag" | "boss"
): "joker" | "tarot" | "planet" | "spectral" | "playing" | "pack" | "voucher" | "tag" | "boss" {
  return type;
}

// ── json-render Component Registry ──
// Maps catalog component names to real React implementations.
// All components accept { props, children, emit } from json-render.

const components = {
  // Layout
  Panel: ({ props, children }: any) => (
    <div
      className="rounded-lg border p-4"
      style={{
        borderColor: "var(--j-border)",
        backgroundColor:
          props.variant === "accent"
            ? "var(--j-accent-muted)"
            : props.variant === "muted"
              ? "var(--j-surface-muted)"
              : "var(--j-surface)",
      }}
    >
      {props.title && (
        <h3
          className="mb-2 font-bold"
          style={{ color: "var(--j-accent)", fontSize: 16 }}
        >
          {props.title}
        </h3>
      )}
      {props.subtitle && (
        <p className="mb-3 text-sm" style={{ color: "var(--j-muted)" }}>
          {props.subtitle}
        </p>
      )}
      {children}
    </div>
  ),

  Stack: ({ props, children }: any) => (
    <div
      className="flex flex-col"
      style={{ gap: props.gap ?? 12, alignItems: props.align ?? "stretch" }}
    >
      {children}
    </div>
  ),

  Grid: ({ props, children }: any) => (
    <div
      className="grid"
      style={{
        gridTemplateColumns: `repeat(${props.columns ?? 3}, minmax(0, 1fr))`,
        gap: props.gap ?? 16,
      }}
    >
      {children}
    </div>
  ),

  // Seed Results
  SeedCard: ({ props, emit }: any) => (
    <div
      className="rounded-lg border p-3 transition-colors hover:border-[var(--j-accent)]"
      style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)", cursor: "pointer" }}
      onClick={() => emit?.("analyzeSeed", { seed: props.seed })}
    >
      <div className="flex items-center justify-between">
        <code
          className="font-mono text-sm font-bold"
          style={{ color: "var(--j-accent)", letterSpacing: "0.04em" }}
        >
          {props.seed}
        </code>
        {props.rank !== undefined && (
          <span
            className="rounded px-1.5 py-0.5 text-xs font-bold"
            style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
          >
            #{props.rank}
          </span>
        )}
      </div>
      {props.score !== undefined && (
        <div className="mt-1 text-sm" style={{ color: "var(--j-muted)" }}>
          Score: <strong style={{ color: "var(--j-foreground)" }}>{props.score}</strong>
        </div>
      )}
      {props.highlights && props.highlights.length > 0 && (
        <div className="mt-2 flex flex-wrap gap-1">
          {props.highlights.map((h: string) => (
            <span
              key={h}
              className="rounded px-1.5 py-0.5 text-xs"
              style={{ backgroundColor: "var(--j-accent-muted)", color: "var(--j-accent)" }}
            >
              {h}
            </span>
          ))}
        </div>
      )}
      {props.jokers && props.jokers.length > 0 && (
        <div className="mt-2 flex gap-1">
          {props.jokers.slice(0, 3).map((j: string) => (
            <span key={j} className="text-xs" style={{ color: "var(--j-muted)" }}>
              {j}
            </span>
          ))}
          {props.jokers.length > 3 && (
            <span className="text-xs" style={{ color: "var(--j-muted)" }}>
              +{props.jokers.length - 3}
            </span>
          )}
        </div>
      )}
      <div className="mt-2 flex gap-2">
        <button
          className="rounded px-2 py-1 text-xs"
          style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
          onClick={(e) => {
            e.stopPropagation();
            emit?.("copySeed", { seed: props.seed });
          }}
        >
          Copy
        </button>
        <button
          className="rounded px-2 py-1 text-xs"
          style={{ border: "1px solid var(--j-border)", color: "var(--j-muted)" }}
          onClick={(e) => {
            e.stopPropagation();
            emit?.("analyzeSeed", { seed: props.seed });
          }}
        >
          Analyze
        </button>
      </div>
    </div>
  ),

  SeedList: ({ props }: any) => (
    <div className="space-y-2">
      {props.seeds?.map((seed: string, i: number) => (
        <div
          key={seed}
          className="flex items-center justify-between rounded border px-3 py-2"
          style={{ borderColor: "var(--j-border)" }}
        >
          <div className="flex items-center gap-3">
            <span className="text-xs font-mono" style={{ color: "var(--j-muted)", minWidth: 30 }}>
              {i + 1}
            </span>
            <code className="font-mono text-sm font-semibold" style={{ color: "var(--j-accent)" }}>
              {seed}
            </code>
          </div>
          {props.scores && props.scores[i] !== undefined && (
            <span className="text-sm font-mono" style={{ color: "var(--j-foreground)" }}>
              {props.scores[i]}
            </span>
          )}
        </div>
      ))}
      {props.total !== undefined && props.seeds.length < props.total && (
        <div className="text-center text-xs py-2" style={{ color: "var(--j-muted)" }}>
          …and {props.total - props.seeds.length} more
        </div>
      )}
    </div>
  ),

  SearchStats: ({ props, emit }: any) => (
    <div
      className="rounded-lg border p-4"
      style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
    >
      <div className="flex items-center justify-between mb-3">
        <div className="flex items-center gap-2">
          <span
            className="inline-block h-2.5 w-2.5 rounded-full"
            style={{
              backgroundColor:
                props.status === "running"
                  ? "#0093ff"
                  : props.status === "completed"
                    ? "#6bff6b"
                    : props.status === "error"
                      ? "#ff6b6b"
                      : "var(--j-muted)",
            }}
          />
          <span className="text-sm font-semibold capitalize" style={{ color: "var(--j-foreground)" }}>
            {props.status}
          </span>
        </div>
        {props.status === "running" && (
          <button
            className="rounded px-3 py-1 text-xs"
            style={{ border: "1px solid #ff6b6b44", color: "#ff6b6b" }}
            onClick={() => emit?.("cancelSearch")}
          >
            Stop
          </button>
        )}
      </div>
      <div className="grid grid-cols-2 gap-3">
        {props.seedsSearched && (
          <div>
            <div className="text-xs" style={{ color: "var(--j-muted)" }}>Searched</div>
            <div className="font-mono text-sm font-semibold" style={{ color: "var(--j-foreground)" }}>
              {props.seedsSearched}
            </div>
          </div>
        )}
        {props.matchesFound !== undefined && (
          <div>
            <div className="text-xs" style={{ color: "var(--j-muted)" }}>Matches</div>
            <div className="font-mono text-sm font-semibold" style={{ color: "var(--j-accent)" }}>
              {props.matchesFound}
            </div>
          </div>
        )}
        {props.seedsPerSecond !== undefined && (
          <div>
            <div className="text-xs" style={{ color: "var(--j-muted)" }}>Speed</div>
            <div className="font-mono text-sm font-semibold" style={{ color: "var(--j-foreground)" }}>
              {props.seedsPerSecond}/s
            </div>
          </div>
        )}
        {props.elapsed && (
          <div>
            <div className="text-xs" style={{ color: "var(--j-muted)" }}>Time</div>
            <div className="font-mono text-sm font-semibold" style={{ color: "var(--j-foreground)" }}>
              {props.elapsed}
            </div>
          </div>
        )}
      </div>
    </div>
  ),

  // Balatro Cards (using jaml-ui JamlGameCard)
  JokerCard: ({ props }: any) => (
    <div className="inline-block">
      <JamlGameCard
        type="joker"
        card={{
          name: props.name,
          edition: props.edition as any,
          isEternal: props.eternal,
          isPerishable: props.perishable,
          isRental: props.rental,
        }}
      />
    </div>
  ),

  TarotCard: ({ props }: any) => (
    <div className="inline-block">
      <JamlGameCard
        type="tarot"
        card={{ name: props.name, edition: props.edition as any }}
      />
    </div>
  ),

  PlanetCard: ({ props }: any) => (
    <div className="inline-block">
      <JamlGameCard
        type="planet"
        card={{ name: props.name, edition: props.edition as any }}
      />
    </div>
  ),

  SpectralCard: ({ props }: any) => (
    <div className="inline-block">
      <JamlGameCard
        type="spectral"
        card={{ name: props.name, edition: props.edition as any }}
      />
    </div>
  ),

  PlayingCard: ({ props }: any) => (
    <div className="inline-block">
      <JamlGameCard
        type="playing"
        card={{
          rank: props.rank,
          suit: props.suit,
          enhancement: props.enhancement as any,
          seal: props.seal as any,
          edition: props.edition as any,
        }}
      />
    </div>
  ),

  // Shop & Route
  ShopQueue: ({ props, emit }: any) => (
    <div
      className="rounded-lg border p-4"
      style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
    >
      <div className="mb-3 flex items-center justify-between">
        <span className="font-bold" style={{ color: "var(--j-accent)", fontSize: 16 }}>
          Ante {props.ante}
        </span>
        {props.rerollCost && (
          <span className="text-xs" style={{ color: "var(--j-muted)" }}>
            Reroll: ${props.rerollCost}
          </span>
        )}
      </div>
      <div className="grid grid-cols-2 gap-2">
        {props.items?.map((item: any, i: number) => (
          <div
            key={i}
            className="rounded border p-2 text-xs"
            style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface-muted)" }}
            onClick={() => emit?.("showAnte", { seed: "", ante: props.ante })}
          >
            <span className="uppercase tracking-wider" style={{ color: "var(--j-muted)", fontSize: 10 }}>
              {item.type}
            </span>
            <div className="font-semibold" style={{ color: "var(--j-foreground)" }}>
              {item.name}
            </div>
            {item.edition && (
              <span className="text-xs" style={{ color: "var(--j-accent)" }}>
                {item.edition}
              </span>
            )}
          </div>
        ))}
      </div>
    </div>
  ),

  BossBlind: ({ props }: any) => (
    <div
      className="rounded-lg border p-4"
      style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
    >
      <div className="mb-2 flex items-center gap-2">
        <span className="rounded px-2 py-0.5 text-xs font-bold" style={{ backgroundColor: "#ff6b6b22", color: "#ff6b6b" }}>
          BOSS
        </span>
        <span className="font-bold" style={{ color: "var(--j-foreground)", fontSize: 16 }}>
          {props.name}
        </span>
      </div>
      {props.description && (
        <p className="text-sm" style={{ color: "var(--j-muted)" }}>
          {props.description}
        </p>
      )}
      {props.debuff && (
        <p className="mt-2 text-xs" style={{ color: "#ff6b6b" }}>
          Debuff: {props.debuff}
        </p>
      )}
      <div className="mt-2 text-xs" style={{ color: "var(--j-muted)" }}>
        Ante {props.ante}
      </div>
    </div>
  ),

  AnteRoute: ({ props, emit }: any) => (
    <div
      className="rounded-lg border p-3 cursor-pointer transition-colors hover:border-[var(--j-accent)]"
      style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
      onClick={() => emit?.("showAnte", { seed: "", ante: props.ante })}
    >
      <div className="flex items-center justify-between">
        <span className="font-bold" style={{ color: "var(--j-accent)" }}>
          Ante {props.ante}
        </span>
        <span className="text-xs font-semibold" style={{ color: "var(--j-foreground)" }}>
          {props.boss}
        </span>
      </div>
      <div className="mt-1 flex gap-3 text-xs" style={{ color: "var(--j-muted)" }}>
        {props.shopItems !== undefined && <span>{props.shopItems} shop</span>}
        {props.packCount !== undefined && <span>{props.packCount} packs</span>}
        {props.tags && props.tags.length > 0 && <span>Tags: {props.tags.join(", ")}</span>}
      </div>
    </div>
  ),

  FullRoute: ({ props, emit }: any) => (
    <div className="space-y-3">
      <div className="mb-2 flex items-center gap-2">
        <code className="font-mono text-sm font-bold" style={{ color: "var(--j-accent)" }}>
          {props.seed}
        </code>
        <span className="text-xs" style={{ color: "var(--j-muted)" }}>
          Full Route
        </span>
      </div>
      <div className="grid grid-cols-1 gap-2">
        {props.antes?.map((ante: any) => (
          <div
            key={ante.ante}
            className="rounded border p-2 cursor-pointer"
            style={{ borderColor: "var(--j-border)" }}
            onClick={() => emit?.("showAnte", { seed: props.seed, ante: ante.ante })}
          >
            <div className="flex items-center justify-between">
              <span className="text-xs font-bold" style={{ color: "var(--j-accent)" }}>
                Ante {ante.ante}
              </span>
              <span className="text-xs" style={{ color: "var(--j-foreground)" }}>
                {ante.boss}
              </span>
            </div>
            <div className="flex gap-2 text-xs" style={{ color: "var(--j-muted)" }}>
              {ante.shopCount !== undefined && <span>{ante.shopCount} shop</span>}
              {ante.packCount !== undefined && <span>{ante.packCount} packs</span>}
            </div>
          </div>
        ))}
      </div>
    </div>
  ),

  // Erratic Deck
  ErraticDeck: ({ props, emit }: any) => (
    <div
      className="rounded-lg border p-4"
      style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface)" }}
    >
      <div className="mb-3 flex items-center justify-between">
        <code className="font-mono text-sm font-bold" style={{ color: "var(--j-accent)" }}>
          {props.seed}
        </code>
        {props.erraticScore !== undefined && (
          <span className="rounded px-2 py-0.5 text-xs font-bold" style={{ backgroundColor: "var(--j-accent)", color: "#000" }}>
            Erratic: {props.erraticScore}
          </span>
        )}
      </div>

      <div className="mb-3 grid grid-cols-5 gap-1">
        {props.cards?.slice(0, 20).map((card: any, i: number) => (
          <div
            key={i}
            className="rounded border p-1 text-center text-xs"
            style={{ borderColor: "var(--j-border)", backgroundColor: "var(--j-surface-muted)" }}
          >
            <div style={{ color: "var(--j-foreground)" }}>{card.rank}</div>
            <div style={{ color: "var(--j-muted)" }}>{card.suit}</div>
          </div>
        ))}
        {props.cards && props.cards.length > 20 && (
          <div className="flex items-center justify-center text-xs" style={{ color: "var(--j-muted)" }}>
            +{props.cards.length - 20}
          </div>
        )}
      </div>

      {(props.suits || props.ranks) && (
        <div className="grid grid-cols-2 gap-3">
          {props.suits && (
            <div>
              <div className="mb-1 text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
                Suits
              </div>
              {Object.entries(props.suits).map(([suit, count]) => (
                <div key={suit} className="flex justify-between text-xs">
                  <span style={{ color: "var(--j-foreground)" }}>{suit}</span>
                  <span className="font-mono" style={{ color: "var(--j-accent)" }}>
                    {count as number}
                  </span>
                </div>
              ))}
            </div>
          )}
          {props.ranks && (
            <div>
              <div className="mb-1 text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
                Ranks
              </div>
              {Object.entries(props.ranks).map(([rank, count]) => (
                <div key={rank} className="flex justify-between text-xs">
                  <span style={{ color: "var(--j-foreground)" }}>{rank}</span>
                  <span className="font-mono" style={{ color: "var(--j-accent)" }}>
                    {count as number}
                  </span>
                </div>
              ))}
            </div>
          )}
        </div>
      )}
    </div>
  ),

  ErraticComparison: ({ props }: any) => (
    <div className="overflow-x-auto">
      <table className="w-full text-sm" style={{ borderCollapse: "collapse" }}>
        <thead>
          <tr style={{ borderBottom: "1px solid var(--j-border)" }}>
            <th className="px-3 py-2 text-left text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
              Seed
            </th>
            <th className="px-3 py-2 text-right text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
              Erratic
            </th>
            <th className="px-3 py-2 text-left text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
              Dom. Suit
            </th>
            <th className="px-3 py-2 text-left text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
              Dom. Rank
            </th>
          </tr>
        </thead>
        <tbody>
          {props.seeds?.map((s: any) => (
            <tr key={s.seed} style={{ borderBottom: "1px solid var(--j-border)" }}>
              <td className="px-3 py-2 font-mono font-semibold" style={{ color: "var(--j-accent)" }}>
                {s.seed}
              </td>
              <td className="px-3 py-2 text-right font-mono font-semibold" style={{ color: "var(--j-foreground)" }}>
                {s.erraticScore}
              </td>
              <td className="px-3 py-2 text-xs" style={{ color: "var(--j-muted)" }}>
                {s.dominantSuit ?? "—"}
              </td>
              <td className="px-3 py-2 text-xs" style={{ color: "var(--j-muted)" }}>
                {s.dominantRank ?? "—"}
              </td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  ),

  // JAML & Filter
  JamlFilter: ({ props }: any) => (
    <div
      className="rounded-lg border p-4 font-mono text-sm"
      style={{
        borderColor: props.isValid === false ? "#ff6b6b" : "var(--j-border)",
        backgroundColor: "var(--j-surface-muted)",
        color: "var(--j-foreground)",
        whiteSpace: "pre-wrap",
      }}
    >
      {props.description && (
        <div className="mb-2 text-xs" style={{ color: "var(--j-muted)" }}>
          {props.description}
        </div>
      )}
      <pre className="m-0">{props.jaml}</pre>
    </div>
  ),

  FilterSuggestion: ({ props, emit }: any) => (
    <div
      className="rounded-lg border p-4"
      style={{ borderColor: "var(--j-accent)", backgroundColor: "var(--j-accent-muted)" }}
    >
      <div className="mb-2 text-sm font-semibold" style={{ color: "var(--j-accent)" }}>
        {props.suggestion}
      </div>
      {props.reason && (
        <p className="mb-3 text-sm" style={{ color: "var(--j-muted)" }}>
          {props.reason}
        </p>
      )}
      {props.jaml && (
        <pre
          className="mb-3 rounded p-2 font-mono text-xs"
          style={{ backgroundColor: "var(--j-surface)", color: "var(--j-foreground)" }}
        >
          {props.jaml}
        </pre>
      )}
      <button
        className="rounded px-3 py-1.5 text-sm font-semibold"
        style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
        onClick={() => emit?.("applyFilter", { jaml: props.jaml })}
      >
        Apply Filter
      </button>
    </div>
  ),

  // Chat & Input
  ChatMessage: ({ props }: any) => (
    <div
      className="mb-3 rounded-lg p-3"
      style={{
        backgroundColor:
          props.role === "user"
            ? "var(--j-surface-muted)"
            : props.role === "assistant"
              ? "var(--j-surface)"
              : "var(--j-surface-muted)",
        borderLeft:
          props.role === "assistant" ? "3px solid var(--j-accent)" : "3px solid transparent",
      }}
    >
      <div className="mb-1 text-xs font-semibold" style={{ color: "var(--j-muted)" }}>
        {props.role === "user" ? "You" : props.role === "assistant" ? "Seed AI" : "System"}
      </div>
      <div className="text-sm" style={{ color: "var(--j-foreground)" }}>
        {props.content}
      </div>
      {props.timestamp && (
        <div className="mt-1 text-xs" style={{ color: "var(--j-muted)" }}>
          {props.timestamp}
        </div>
      )}
    </div>
  ),

  ChatInput: ({ props, emit }: any) => (
    <div className="flex gap-2">
      <input
        type="text"
        className="flex-1 rounded border px-3 py-2 text-sm"
        style={{
          borderColor: "var(--j-border)",
          backgroundColor: "var(--j-surface)",
          color: "var(--j-foreground)",
        }}
        placeholder={props.placeholder ?? "Describe the seed you want…"}
        disabled={props.disabled}
        onKeyDown={(e) => {
          if (e.key === "Enter" && !props.disabled) {
            emit?.("submitChat", { message: (e.target as HTMLInputElement).value });
          }
        }}
      />
      <button
        className="rounded px-4 py-2 text-sm font-semibold"
        style={{ backgroundColor: "var(--j-accent)", color: "#000" }}
        disabled={props.disabled}
        onClick={() => {
          const input = document.querySelector('input[type="text"]') as HTMLInputElement;
          if (input?.value) {
            emit?.("submitChat", { message: input.value });
          }
        }}
      >
        Send
      </button>
    </div>
  ),

  ActionButton: ({ props, emit }: any) => (
    <button
      className="rounded px-4 py-2 text-sm font-semibold transition-opacity"
      style={{
        backgroundColor:
          props.variant === "primary"
            ? "var(--j-accent)"
            : props.variant === "secondary"
              ? "var(--j-surface-muted)"
              : props.variant === "danger"
                ? "#ff6b6b22"
                : "transparent",
        color:
          props.variant === "primary"
            ? "#000"
            : props.variant === "danger"
              ? "#ff6b6b"
              : "var(--j-foreground)",
        border:
          props.variant === "ghost"
            ? "none"
            : props.variant === "secondary"
              ? "1px solid var(--j-border)"
              : "none",
      }}
      onClick={() => emit?.("click")}
    >
      {props.icon && <span className="mr-1">{props.icon}</span>}
      {props.label}
    </button>
  ),

  // Typography
  Heading: ({ props, children }: any) => {
    const Tag = `h${props.level ?? 2}` as any;
    return (
      <Tag
        className="font-bold"
        style={{
          color:
            props.color === "accent"
              ? "var(--j-accent)"
              : props.color === "muted"
                ? "var(--j-muted)"
                : "var(--j-foreground)",
          fontSize: props.level === 1 ? 28 : props.level === 2 ? 22 : props.level === 3 ? 18 : 16,
          marginBottom: 8,
        }}
      >
        {props.text}
        {children}
      </Tag>
    );
  },

  Text: ({ props, children }: any) => (
    <p
      className="text-sm leading-relaxed"
      style={{
        color:
          props.variant === "muted"
            ? "var(--j-muted)"
            : props.variant === "accent"
              ? "var(--j-accent)"
              : props.variant === "code"
                ? "var(--j-foreground)"
                : "var(--j-foreground)",
        fontFamily: props.variant === "code" ? "var(--j-font-code), monospace" : undefined,
      }}
    >
      {props.body}
      {children}
    </p>
  ),

  Badge: ({ props }: any) => {
    const colors: Record<string, { bg: string; text: string }> = {
      default: { bg: "var(--j-surface-muted)", text: "var(--j-muted)" },
      success: { bg: "#6bff6b22", text: "#6bff6b" },
      warning: { bg: "#e4b64322", text: "#e4b643" },
      error: { bg: "#ff6b6b22", text: "#ff6b6b" },
      info: { bg: "#0093ff22", text: "#0093ff" },
    };
    const c = colors[props.variant ?? "default"];
    return (
      <span
        className="inline-block rounded px-2 py-0.5 text-xs font-semibold"
        style={{ backgroundColor: c.bg, color: c.text }}
      >
        {props.label}
      </span>
    );
  },
};

// ── Build Registry ──
export const { registry } = defineRegistry(balatroCatalog, { components });

// ── Re-exports ──
export { balatroCatalog };
