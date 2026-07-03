import { type FC, useState } from "react";
import { Panel, Stack, Text, Badge, type BadgeTone } from "./layout.js";

/**
 * Domain components — SearchStats, ErrorBanner, LoadingPulse, SeedCard, etc.
 *
 * These are Balatro-specific UI components used by the json-render system.
 * All use CSS tokens and the layout primitives above.
 */

/* ─── SearchStats ─── */
export interface SearchStatsProps {
  status: "idle" | "running" | "completed" | "error";
  seedsSearched?: string;
  matchesFound?: number;
  seedsPerSecond?: number;
  elapsed?: string;
  className?: string;
}

export const SearchStats: FC<SearchStatsProps> = ({
  status,
  seedsSearched,
  matchesFound,
  seedsPerSecond,
  elapsed,
  className = "",
}) => {
  const statusColor =
    status === "running"
      ? "var(--j-blue)"
      : status === "completed"
        ? "var(--j-green)"
        : status === "error"
          ? "var(--j-red)"
          : "var(--j-grey)";

  const statusLabel =
    status === "idle"
      ? "Ready"
      : status === "running"
        ? "Searching..."
        : status === "completed"
          ? "Done"
          : "Error";

  return (
    <Panel className={className} variant="accent">
      <Stack gap={8}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            gap: 8,
          }}
        >
          <div
            style={{
              width: 8,
              height: 8,
              borderRadius: "50%",
              background: statusColor,
              boxShadow: `0 0 8px ${statusColor}`,
            }}
          />
          <Text body={statusLabel} variant="accent" />
          {status === "running" && (
            <span
              style={{
                animation: "pulse 1.5s infinite",
                color: "var(--j-blue)",
              }}
            >
              ...
            </span>
          )}
        </div>

        <div
          style={{
            display: "grid",
            gridTemplateColumns: "repeat(auto-fit, minmax(120px, 1fr))",
            gap: 12,
          }}
        >
          {seedsSearched !== undefined && (
            <div>
              <Text body="Searched" variant="muted" />
              <Text body={seedsSearched} variant="title" />
            </div>
          )}
          {matchesFound !== undefined && (
            <div>
              <Text body="Matches" variant="muted" />
              <Text body={String(matchesFound)} variant="title" />
            </div>
          )}
          {seedsPerSecond !== undefined && (
            <div>
              <Text body="Speed" variant="muted" />
              <Text body={`${seedsPerSecond.toLocaleString()}/s`} variant="title" />
            </div>
          )}
          {elapsed && (
            <div>
              <Text body="Time" variant="muted" />
              <Text body={elapsed} variant="title" />
            </div>
          )}
        </div>
      </Stack>
    </Panel>
  );
};

/* ─── ErrorBanner ─── */
export interface ErrorBannerProps {
  message: string;
  onDismiss?: boolean;
  className?: string;
}

export const ErrorBanner: FC<ErrorBannerProps> = ({
  message,
  onDismiss,
  className = "",
}) => {
  const [dismissed, setDismissed] = useState(false);
  if (dismissed) return null;

  return (
    <div
      className={className}
      style={{
        border: "2px solid var(--j-red)",
        borderRadius: "var(--j-radius)",
        background: "var(--j-dark-red)",
        padding: "var(--j-space-3) var(--j-space-4)",
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 12,
      }}
    >
      <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
        <span style={{ color: "var(--j-red)", fontSize: "var(--j-text-lg)" }}>⚠</span>
        <Text body={message} variant="error" />
      </div>
      {onDismiss && (
        <button
          onClick={() => setDismissed(true)}
          style={{
            background: "none",
            border: "none",
            color: "var(--j-red)",
            cursor: "pointer",
            fontSize: "var(--j-text-lg)",
            padding: 0,
            lineHeight: 1,
          }}
          aria-label="Dismiss error"
        >
          ×
        </button>
      )}
    </div>
  );
};

/* ─── LoadingPulse ─── */
export interface LoadingPulseProps {
  text?: string;
  className?: string;
}

export const LoadingPulse: FC<LoadingPulseProps> = ({
  text = "Loading...",
  className = "",
}) => {
  return (
    <div
      className={className}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        gap: 12,
        padding: "var(--j-space-5)",
      }}
    >
      <div
        style={{
          width: 16,
          height: 16,
          borderRadius: "50%",
          background: "var(--j-blue)",
          animation: "pulse 1.5s infinite",
          boxShadow: "0 0 12px var(--j-blue)",
        }}
      />
      <Text body={text} variant="muted" />
    </div>
  );
};

/* ─── SeedCard ─── */
export interface SeedCardProps {
  seed: string;
  score?: number;
  rank?: number;
  highlights?: string[];
  jokers?: string[];
  edition?: string;
  onClick?: boolean; // semantic: card is interactive
  className?: string;
}

export const SeedCard: FC<SeedCardProps> = ({
  seed,
  score,
  rank,
  highlights,
  jokers,
  edition,
  onClick,
  className = "",
}) => {
  return (
    <div
      className={className}
      onClick={onClick ? () => navigator.clipboard?.writeText(seed) : undefined}
      role={onClick ? "button" : undefined}
      tabIndex={onClick ? 0 : undefined}
      style={{
        border: "2px solid var(--j-panel-edge)",
        borderRadius: "var(--j-radius)",
        background: "var(--j-surface-inset)",
        padding: "var(--j-space-3)",
        cursor: onClick ? "pointer" : "default",
        transition: "var(--j-transition)",
        position: "relative",
      }}
      onMouseEnter={(e) => {
        if (onClick) {
          e.currentTarget.style.borderColor = "var(--j-blue)";
          e.currentTarget.style.transform = "translateY(-2px)";
          e.currentTarget.style.boxShadow = "var(--j-shadow)";
        }
      }}
      onMouseLeave={(e) => {
        if (onClick) {
          e.currentTarget.style.borderColor = "var(--j-panel-edge)";
          e.currentTarget.style.transform = "translateY(0)";
          e.currentTarget.style.boxShadow = "none";
        }
      }}
    >
      <Stack gap={8}>
        <div
          style={{
            display: "flex",
            alignItems: "center",
            justifyContent: "space-between",
          }}
        >
          <div style={{ display: "flex", alignItems: "center", gap: 8 }}>
            {rank !== undefined && (
              <span
                style={{
                  color: "var(--j-gold)",
                  fontSize: "var(--j-text-sm)",
                  fontWeight: 700,
                  minWidth: 24,
                }}
              >
                #{rank}
              </span>
            )}
            <Text body={seed} variant="accent" />
          </div>
          {score !== undefined && (
            <Badge label={String(score)} tone="gold" />
          )}
        </div>

        {edition && <Badge label={edition} tone="purple" />}

        {highlights && highlights.length > 0 && (
          <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
            {highlights.map((h, i) => (
              <Badge key={i} label={h} tone="green" />
            ))}
          </div>
        )}

        {jokers && jokers.length > 0 && (
          <div style={{ display: "flex", flexWrap: "wrap", gap: 6 }}>
            {jokers.map((j, i) => (
              <Badge key={i} label={j} tone="blue" />
            ))}
          </div>
        )}
      </Stack>
    </div>
  );
};

/* ─── SeedList ─── */
export interface SeedListProps {
  seeds: string[];
  scores?: number[];
  total?: number;
  pageSize?: number;
  className?: string;
}

export const SeedList: FC<SeedListProps> = ({
  seeds,
  scores,
  total,
  pageSize = 20,
  className = "",
}) => {
  const [page, setPage] = useState(0);
  const maxPage = Math.ceil((total ?? seeds.length) / pageSize) - 1;
  const start = page * pageSize;
  const visible = seeds.slice(start, start + pageSize);

  return (
    <Stack gap={12} className={className}>
      {visible.map((seed, i) => (
        <SeedCard
          key={seed}
          seed={seed}
          score={scores?.[start + i]}
          rank={start + i + 1}
          onClick={true}
        />
      ))}

      {maxPage > 0 && (
        <div
          style={{
            display: "flex",
            justifyContent: "center",
            gap: 12,
            alignItems: "center",
            padding: "var(--j-space-3) 0",
          }}
        >
          <button
            disabled={page <= 0}
            onClick={() => setPage((p) => p - 1)}
            style={{
              background: "var(--j-surface-inset)",
              border: "2px solid var(--j-panel-edge)",
              color: "var(--j-grey)",
              padding: "4px 12px",
              borderRadius: "var(--j-radius)",
              cursor: page <= 0 ? "not-allowed" : "pointer",
              opacity: page <= 0 ? 0.5 : 1,
              fontFamily: "var(--j-font)",
            }}
          >
            ← Prev
          </button>
          <Text
            body={`${page + 1} / ${maxPage + 1}`}
            variant="muted"
          />
          <button
            disabled={page >= maxPage}
            onClick={() => setPage((p) => p + 1)}
            style={{
              background: "var(--j-surface-inset)",
              border: "2px solid var(--j-panel-edge)",
              color: "var(--j-grey)",
              padding: "4px 12px",
              borderRadius: "var(--j-radius)",
              cursor: page >= maxPage ? "not-allowed" : "pointer",
              opacity: page >= maxPage ? 0.5 : 1,
              fontFamily: "var(--j-font)",
            }}
          >
            Next →
          </button>
        </div>
      )}
    </Stack>
  );
};

/* ─── JokerBadge ─── */
export interface JokerBadgeProps {
  name: string;
  edition?: "Foil" | "Holographic" | "Polychrome" | "Negative";
  rarity?: "Common" | "Uncommon" | "Rare" | "Legendary";
  className?: string;
}

export const JokerBadge: FC<JokerBadgeProps> = ({
  name,
  edition,
  rarity,
  className = "",
}) => {
  const rarityTone: Record<string, string> = {
    Common: "grey",
    Uncommon: "green",
    Rare: "red",
    Legendary: "gold",
  };

  const editionTone: Record<string, string> = {
    Foil: "blue",
    Holographic: "green",
    Polychrome: "purple",
    Negative: "red",
  };

  return (
    <div className={className} style={{ display: "flex", alignItems: "center", gap: 6 }}>
      <Badge label={name} tone={(rarityTone[rarity ?? ""] as BadgeTone) || "grey"} />
      {edition && (
        <Badge label={edition} tone={(editionTone[edition] as BadgeTone) || "grey"} />
      )}
    </div>
  );
};

/* ─── EditionBadge ─── */
export interface EditionBadgeProps {
  edition: "Foil" | "Holographic" | "Polychrome" | "Negative";
  className?: string;
}

export const EditionBadge: FC<EditionBadgeProps> = ({
  edition,
  className = "",
}) => {
  const map: Record<string, string> = {
    Foil: "blue",
    Holographic: "green",
    Polychrome: "purple",
    Negative: "red",
  };

  return <Badge className={className} label={edition} tone={(map[edition] as BadgeTone) || "grey"} />;
};
