import { type FC, useState } from "react";
import { FiAlertTriangle, FiChevronLeft, FiChevronRight, FiX } from "react-icons/fi";
import { Panel, Stack, Text, Badge, type BadgeTone } from "./layout.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboButton } from "../../ui/JimboButton.js";
import { JimboIconButton } from "../../ui/JimboIconButton.js";

/**
 * Domain components — SearchStats, ErrorBanner, LoadingPulse, SeedCard, etc.
 *
 * Balatro-specific json-render nodes. Layout is grid, never flex (host iframes
 * size flex differently per host — see CLAUDE.md rule #1), interactive controls
 * are the real Jimbo button primitives, and every spacing/radius value comes from
 * a real --j-* token. An earlier pass used flex, raw <button>s, an emoji, and
 * invented tokens (--j-radius, --j-space-3, --j-text-lg) that render as fallback.
 */

/* Reusable grid rows — the no-flex replacements for the old flex rows. */
const ROW: React.CSSProperties = {
  display: "grid",
  gridAutoFlow: "column",
  gridAutoColumns: "max-content",
  alignItems: "center",
};
// Wraps pills onto more rows without flex-wrap: auto-fit grid tracks sized to content.
const PILLS: React.CSSProperties = {
  display: "grid",
  gridTemplateColumns: "repeat(auto-fit, minmax(64px, max-content))",
  gap: 6,
  justifyContent: "start",
};

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
        <div style={{ ...ROW, gap: 8, justifyContent: "start" }}>
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
            <span style={{ animation: "pulse 1.5s infinite", color: "var(--j-blue)" }}>...</span>
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

export const ErrorBanner: FC<ErrorBannerProps> = ({ message, onDismiss, className = "" }) => {
  const [dismissed, setDismissed] = useState(false);
  if (dismissed) return null;

  return (
    <div
      className={className}
      style={{
        border: "2px solid var(--j-red)",
        borderRadius: "var(--j-radius-lg)",
        background: "var(--j-dark-red)",
        padding: "var(--j-space-lg) var(--j-space-xl)",
        display: "grid",
        gridTemplateColumns: "1fr auto",
        alignItems: "center",
        gap: 12,
      }}
    >
      <div style={{ ...ROW, gap: 8 }}>
        <FiAlertTriangle color="var(--j-red)" aria-hidden />
        <Text body={message} variant="error" />
      </div>
      {onDismiss && (
        <JimboIconButton
          size="sm"
          tone="destructive"
          aria-label="Dismiss error"
          onClick={() => setDismissed(true)}
        >
          <FiX />
        </JimboIconButton>
      )}
    </div>
  );
};

/* ─── LoadingPulse ─── */
export interface LoadingPulseProps {
  text?: string;
  className?: string;
}

export const LoadingPulse: FC<LoadingPulseProps> = ({ text = "Loading...", className = "" }) => (
  <div
    className={className}
    style={{ ...ROW, gap: 12, justifyContent: "center", padding: "var(--j-space-xl)" }}
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
        borderRadius: "var(--j-radius-lg)",
        background: "var(--j-surface-inset)",
        padding: "var(--j-space-lg)",
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
        <div style={{ display: "grid", gridTemplateColumns: "1fr auto", alignItems: "center" }}>
          <div style={{ ...ROW, gap: 8 }}>
            {rank !== undefined && (
              <JimboText size="sm" tone="gold">
                #{rank}
              </JimboText>
            )}
            <Text body={seed} variant="accent" />
          </div>
          {score !== undefined && <Badge label={String(score)} tone="gold" />}
        </div>

        {edition && <Badge label={edition} tone="purple" />}

        {highlights && highlights.length > 0 && (
          <div style={PILLS}>
            {highlights.map((h, i) => (
              <Badge key={i} label={h} tone="green" />
            ))}
          </div>
        )}

        {jokers && jokers.length > 0 && (
          <div style={PILLS}>
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
        <div style={{ ...ROW, gap: 12, justifyContent: "center", padding: "var(--j-space-lg) 0" }}>
          <JimboButton
            size="sm"
            tone="blue"
            disabled={page <= 0}
            onClick={() => setPage((p) => p - 1)}
          >
            <FiChevronLeft /> Prev
          </JimboButton>
          <Text body={`${page + 1} / ${maxPage + 1}`} variant="muted" />
          <JimboButton
            size="sm"
            tone="blue"
            disabled={page >= maxPage}
            onClick={() => setPage((p) => p + 1)}
          >
            Next <FiChevronRight />
          </JimboButton>
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

export const JokerBadge: FC<JokerBadgeProps> = ({ name, edition, rarity, className = "" }) => {
  const rarityTone: Record<string, BadgeTone> = {
    Common: "grey",
    Uncommon: "green",
    Rare: "red",
    Legendary: "gold",
  };

  const editionTone: Record<string, BadgeTone> = {
    Foil: "blue",
    Holographic: "green",
    Polychrome: "purple",
    Negative: "red",
  };

  return (
    <div className={className} style={{ ...ROW, gap: 6 }}>
      <Badge label={name} tone={rarityTone[rarity ?? ""] ?? "grey"} />
      {edition && <Badge label={edition} tone={editionTone[edition] ?? "grey"} />}
    </div>
  );
};

/* ─── EditionBadge ─── */
export interface EditionBadgeProps {
  edition: "Foil" | "Holographic" | "Polychrome" | "Negative";
  className?: string;
}

export const EditionBadge: FC<EditionBadgeProps> = ({ edition, className = "" }) => {
  const map: Record<string, BadgeTone> = {
    Foil: "blue",
    Holographic: "green",
    Polychrome: "purple",
    Negative: "red",
  };

  return <Badge className={className} label={edition} tone={map[edition] ?? "grey"} />;
};
