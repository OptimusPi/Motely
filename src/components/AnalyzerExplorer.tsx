"use client";

import React, { useCallback, useEffect, useRef, useState } from "react";
import {
  JamlBoss,
  JamlGameCard,
  JamlTag,
  JamlVoucher,
  resolveAnalyzerShopItem,
  type AnalyzerShopItem,
} from "./GameCard.js";

export interface AnalyzerBadge {
  label: string;
  tone?: "default" | "accent" | "muted";
}

export interface AnalyzerFact {
  label: string;
  value: string;
}

export interface AnalyzerItem extends AnalyzerShopItem {
  desired?: boolean;
  badges?: AnalyzerBadge[];
  detail?: string;
}

export interface AnalyzerAnteView {
  ante: number;
  boss?: string;
  voucher?: string;
  smallBlindTag?: string;
  bigBlindTag?: string;
  packs?: string[];
  shop?: AnalyzerItem[];
  facts?: AnalyzerFact[];
}

export interface AnalyzerHighlight {
  id: string;
  ante: number;
  title: string;
  subtitle?: string;
  desired?: boolean;
  item?: AnalyzerItem;
  boss?: string;
  voucher?: string;
  tag?: string;
  badges?: AnalyzerBadge[];
}

export interface AnalyzerExplorerProps {
  antes: AnalyzerAnteView[];
  highlights?: AnalyzerHighlight[];
  visibleAntes?: number;
  totalAntes?: number;
  className?: string;
}

export function AnalyzerExplorer({
  antes,
  highlights = [],
  visibleAntes,
  totalAntes,
  className = "",
}: AnalyzerExplorerProps) {
  const [currentAnte, setCurrentAnte] = useState(antes[0]?.ante ?? 0);
  const scrollRef = useRef<HTMLDivElement>(null);
  const anteRefs = useRef<Map<number, HTMLDivElement>>(new Map());
  const highlightRefs = useRef<Map<string, HTMLButtonElement>>(new Map());

  useEffect(() => {
    setCurrentAnte(antes[0]?.ante ?? 0);
  }, [antes]);

  useEffect(() => {
    const root = scrollRef.current;
    if (!root) {
      return;
    }

    const observer = new IntersectionObserver(
      (entries) => {
        const mostVisibleEntry = entries
          .filter((entry) => entry.isIntersecting)
          .sort((left, right) => right.intersectionRatio - left.intersectionRatio)[0];

        if (!mostVisibleEntry) {
          return;
        }

        const ante = Number((mostVisibleEntry.target as HTMLElement).dataset.ante);
        if (!Number.isNaN(ante)) {
          setCurrentAnte(ante);
        }
      },
      {
        root,
        threshold: [0.45, 0.72, 0.9],
      },
    );

    for (const [, element] of anteRefs.current) {
      observer.observe(element);
    }

    return () => observer.disconnect();
  }, [antes]);

  useEffect(() => {
    const activeHighlight = highlights.find((highlight) => highlight.ante === currentAnte);
    if (!activeHighlight) {
      return;
    }

    const element = highlightRefs.current.get(activeHighlight.id);
    element?.scrollIntoView({
      behavior: "smooth",
      inline: "center",
      block: "nearest",
    });
  }, [currentAnte, highlights]);

  const scrollToAnte = useCallback((ante: number) => {
    anteRefs.current.get(ante)?.scrollIntoView({
      behavior: "smooth",
      block: "start",
    });
  }, []);

  const currentAnteIndex = antes.findIndex((ante) => ante.ante === currentAnte);
  const previousAnte = currentAnteIndex > 0 ? antes[currentAnteIndex - 1]?.ante ?? null : null;
  const nextAnte = currentAnteIndex >= 0 && currentAnteIndex < antes.length - 1 ? antes[currentAnteIndex + 1]?.ante ?? null : null;
  const shownAntes = visibleAntes ?? antes.length;
  const availableAntes = totalAntes ?? shownAntes;

  return (
    <div className={className} style={styles.root}>
      {highlights.length > 0 ? (
        <section style={styles.highlightSection}>
          <div style={styles.highlightHeader}>
            <span style={styles.highlightTitle}>Highlights</span>
            <span style={styles.highlightSubtitle}>Swipe, tap, jump</span>
          </div>

          <div style={styles.highlightRail}>
            {highlights.map((highlight) => {
              const isActive = highlight.ante === currentAnte;

              return (
                <button
                  key={highlight.id}
                  ref={(element) => {
                    if (element) {
                      highlightRefs.current.set(highlight.id, element);
                    } else {
                      highlightRefs.current.delete(highlight.id);
                    }
                  }}
                  type="button"
                  onClick={() => scrollToAnte(highlight.ante)}
                  style={isActive ? styles.highlightCardActive : styles.highlightCard}
                >
                  <div style={styles.highlightVisual}>{renderHighlightVisual(highlight)}</div>
                  <div style={styles.highlightMeta}>
                    <span style={styles.highlightAnte}>Ante {highlight.ante}</span>
                    <span style={styles.highlightLabel}>{highlight.title}</span>
                    {highlight.subtitle ? (
                      <span style={styles.highlightDescription}>{highlight.subtitle}</span>
                    ) : null}
                    <BadgeRow badges={highlight.badges} desired={highlight.desired} />
                  </div>
                </button>
              );
            })}
          </div>
        </section>
      ) : null}

      <div style={styles.navBar}>
        <button
          type="button"
          onClick={() => previousAnte !== null && scrollToAnte(previousAnte)}
          disabled={previousAnte === null}
          style={{
            ...styles.navButton,
            opacity: previousAnte !== null ? 1 : 0.25,
          }}
        >
          ▲
        </button>

        <div style={styles.navLabel}>
          Ante {currentAnte}
          <span style={styles.navSubLabel}>
            of {shownAntes}
            {availableAntes > shownAntes ? ` / ${availableAntes}` : ""}
          </span>
        </div>

        <button
          type="button"
          onClick={() => nextAnte !== null && scrollToAnte(nextAnte)}
          disabled={nextAnte === null}
          style={{
            ...styles.navButton,
            opacity: nextAnte !== null ? 1 : 0.25,
          }}
        >
          ▼
        </button>
      </div>

      <div ref={scrollRef} style={styles.scrollRegion}>
        {antes.map((ante) => (
          <div
            key={ante.ante}
            data-ante={ante.ante}
            ref={(element) => {
              if (element) {
                anteRefs.current.set(ante.ante, element);
              } else {
                anteRefs.current.delete(ante.ante);
              }
            }}
            style={styles.antePage}
          >
            <AnteSection ante={ante} />
          </div>
        ))}
      </div>
    </div>
  );
}

function AnteSection({ ante }: { ante: AnalyzerAnteView }) {
  return (
    <section style={styles.anteSection}>
      <div style={styles.anteHeader}>
        <span style={styles.anteHeading}>Ante {ante.ante}</span>
        {ante.boss ? (
          <div style={styles.bossRow}>
            <JamlBoss bossName={ante.boss} scale={0.62} />
            <span style={styles.bossName}>{ante.boss}</span>
          </div>
        ) : null}
      </div>

      {ante.smallBlindTag || ante.bigBlindTag ? (
        <AnalyzerRow label="Tags">
          {ante.smallBlindTag ? (
            <CompactCard
              label="Small"
              visual={<JamlTag tagName={ante.smallBlindTag} scale={0.58} hoverTilt />}
              text={ante.smallBlindTag}
            />
          ) : null}
          {ante.bigBlindTag ? (
            <CompactCard
              label="Big"
              visual={<JamlTag tagName={ante.bigBlindTag} scale={0.58} hoverTilt />}
              text={ante.bigBlindTag}
            />
          ) : null}
        </AnalyzerRow>
      ) : null}

      {ante.voucher ? (
        <AnalyzerRow label="Voucher">
          <CompactCard
            visual={<JamlVoucher voucherName={ante.voucher} scale={0.58} hoverTilt />}
            text={ante.voucher}
          />
        </AnalyzerRow>
      ) : null}

      {ante.shop && ante.shop.length > 0 ? (
        <AnalyzerRow label="Shop" dense>
          {ante.shop.map((item) => (
            <ResolvedItemCard key={`${ante.ante}-${item.id}-${item.name}`} item={item} />
          ))}
        </AnalyzerRow>
      ) : null}

      {ante.packs && ante.packs.length > 0 ? (
        <AnalyzerRow label="Packs">
          {ante.packs.map((pack) => (
            <div key={`${ante.ante}-${pack}`} style={styles.packChip}>
              {pack}
            </div>
          ))}
        </AnalyzerRow>
      ) : null}

      {ante.facts && ante.facts.length > 0 ? (
        <AnalyzerRow label="Facts">
          {ante.facts.map((fact) => (
            <div key={`${ante.ante}-${fact.label}-${fact.value}`} style={styles.factCard}>
              <span style={styles.factLabel}>{fact.label}</span>
              <span style={styles.factValue}>{fact.value}</span>
            </div>
          ))}
        </AnalyzerRow>
      ) : null}
    </section>
  );
}

function AnalyzerRow({
  label,
  children,
  dense = false,
}: {
  label: string;
  children: React.ReactNode;
  dense?: boolean;
}) {
  return (
    <div style={styles.row}>
      <div style={styles.rowLabel}>{label}</div>
      <div style={dense ? styles.denseGrid : styles.cardFlow}>{children}</div>
    </div>
  );
}

function CompactCard({
  label,
  visual,
  text,
}: {
  label?: string;
  visual: React.ReactNode;
  text: string;
}) {
  return (
    <div style={styles.compactCard}>
      {label ? <span style={styles.compactLabel}>{label}</span> : null}
      {visual}
      <span style={styles.itemText}>{text}</span>
    </div>
  );
}

function ResolvedItemCard({ item }: { item: AnalyzerItem }) {
  const resolved = resolveAnalyzerShopItem(item, 0.58);
  const wrapperStyle = item.desired ? styles.itemCardDesired : styles.itemCard;

  if (resolved.kind === "joker" || resolved.kind === "consumable" || resolved.kind === "playing") {
    return (
      <div style={wrapperStyle}>
        <JamlGameCard card={resolved.card} type={resolved.type} hoverTilt />
        <span style={styles.itemText}>{item.name}</span>
        <BadgeRow badges={item.badges} desired={item.desired} />
        {item.detail ? <span style={styles.itemDetail}>{item.detail}</span> : null}
      </div>
    );
  }

  if (resolved.kind === "voucher") {
    return (
      <div style={wrapperStyle}>
        <JamlVoucher voucherName={resolved.voucherName} scale={0.58} hoverTilt />
        <span style={styles.itemText}>{item.name}</span>
        <BadgeRow badges={item.badges} desired={item.desired} />
        {item.detail ? <span style={styles.itemDetail}>{item.detail}</span> : null}
      </div>
    );
  }

  return (
    <div style={wrapperStyle}>
      <div style={styles.packChip}>{item.name}</div>
      <BadgeRow badges={item.badges} desired={item.desired} />
      {item.detail ? <span style={styles.itemDetail}>{item.detail}</span> : null}
    </div>
  );
}

function BadgeRow({ badges, desired = false }: { badges?: AnalyzerBadge[]; desired?: boolean }) {
  const combinedBadges = desired
    ? [{ label: "desired", tone: "accent" as const }, ...(badges ?? [])]
    : (badges ?? []);

  if (combinedBadges.length === 0) {
    return null;
  }

  return (
    <div style={styles.badgeRow}>
      {combinedBadges.map((badge) => {
        const badgeStyle =
          badge.tone === "accent"
            ? styles.badgeAccent
            : badge.tone === "muted"
              ? styles.badgeMuted
              : styles.badge;

        return (
          <span key={`${badge.label}-${badge.tone ?? "default"}`} style={badgeStyle}>
            {badge.label}
          </span>
        );
      })}
    </div>
  );
}

function renderHighlightVisual(highlight: AnalyzerHighlight) {
  if (highlight.item) {
    return <ResolvedItemCard item={highlight.item} />;
  }

  if (highlight.boss) {
    return <JamlBoss bossName={highlight.boss} scale={0.92} hoverTilt />;
  }

  if (highlight.voucher) {
    return <JamlVoucher voucherName={highlight.voucher} scale={0.92} hoverTilt />;
  }

  if (highlight.tag) {
    return <JamlTag tagName={highlight.tag} scale={0.92} hoverTilt />;
  }

  return <div style={styles.packChip}>{highlight.title}</div>;
}

const styles: Record<string, React.CSSProperties> = {
  root: {
    display: "flex",
    flexDirection: "column",
    height: "100%",
    minHeight: 0,
    fontFamily: "m6x11plus, monospace",
  },
  highlightSection: {
    padding: "6px 8px",
    borderBottom: "1px solid #1a1a34",
    background: "#0f0f22",
  },
  highlightHeader: {
    display: "flex",
    alignItems: "center",
    justifyContent: "space-between",
    gap: 8,
    marginBottom: 4,
  },
  highlightTitle: {
    fontSize: 12,
    color: "#6f6fa1",
    textTransform: "uppercase",
    letterSpacing: "0.05em",
  },
  highlightSubtitle: {
    fontSize: 11,
    color: "#6f6fa1",
  },
  highlightRail: {
    display: "flex",
    gap: 6,
    overflowX: "auto",
    paddingBottom: 2,
    scrollSnapType: "x mandatory",
  },
  highlightCard: {
    minWidth: 120,
    maxWidth: 120,
    display: "flex",
    flexDirection: "column",
    gap: 4,
    padding: 4,
    borderRadius: 8,
    border: "1px solid #2a2a55",
    background: "#131326",
    scrollSnapAlign: "center",
    cursor: "pointer",
  },
  highlightCardActive: {
    minWidth: 120,
    maxWidth: 120,
    display: "flex",
    flexDirection: "column",
    gap: 4,
    padding: 4,
    borderRadius: 8,
    border: "1px solid rgba(245,200,66,0.45)",
    background: "rgba(245,200,66,0.08)",
    scrollSnapAlign: "center",
    cursor: "pointer",
  },
  highlightVisual: {
    display: "flex",
    justifyContent: "center",
    alignItems: "flex-start",
    minHeight: 80,
  },
  highlightMeta: {
    display: "flex",
    flexDirection: "column",
    gap: 2,
    alignItems: "flex-start",
  },
  highlightAnte: {
    fontSize: 11,
    color: "#a855f7",
    textTransform: "uppercase",
    letterSpacing: "0.05em",
  },
  highlightLabel: {
    fontSize: 13,
    color: "#ececff",
    lineHeight: 1.2,
    textAlign: "left",
  },
  highlightDescription: {
    fontSize: 11,
    color: "#8e8eb6",
    lineHeight: 1.2,
    textAlign: "left",
  },
  navBar: {
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    gap: 8,
    padding: "4px 8px",
    background: "#0f0f22",
    borderBottom: "1px solid #1a1a34",
  },
  navButton: {
    background: "none",
    border: "1px solid #2a2a55",
    borderRadius: 4,
    color: "#9898c0",
    fontSize: 14,
    padding: "2px 8px",
    cursor: "pointer",
  },
  navLabel: {
    fontSize: 13,
    color: "#a855f7",
    textTransform: "uppercase",
    letterSpacing: "0.05em",
    minWidth: 110,
    textAlign: "center",
  },
  navSubLabel: {
    color: "#5a5a88",
    textTransform: "none",
    letterSpacing: "normal",
    fontSize: 11,
    marginLeft: 4,
  },
  scrollRegion: {
    flex: 1,
    overflowY: "auto",
    overflowX: "hidden",
    scrollSnapType: "y mandatory",
    scrollBehavior: "smooth",
  },
  antePage: {
    scrollSnapAlign: "start",
    scrollSnapStop: "always",
    minHeight: "100%",
    boxSizing: "border-box",
    paddingBottom: 8,
    borderBottom: "1px solid #1a1a34",
  },
  anteSection: {
    paddingBottom: 4,
  },
  anteHeader: {
    display: "flex",
    alignItems: "center",
    gap: 8,
    padding: "6px 8px",
  },
  anteHeading: {
    fontSize: 14,
    color: "#a855f7",
    textTransform: "uppercase",
    letterSpacing: "0.05em",
  },
  bossRow: {
    display: "flex",
    alignItems: "center",
    gap: 4,
  },
  bossName: {
    fontSize: 14,
    color: "#e84040",
  },
  row: {
    padding: "2px 8px 4px",
  },
  rowLabel: {
    fontSize: 11,
    color: "#5a5a88",
    textTransform: "uppercase",
    letterSpacing: "0.05em",
    marginBottom: 4,
  },
  cardFlow: {
    display: "flex",
    flexWrap: "wrap",
    gap: 6,
    alignItems: "flex-start",
  },
  denseGrid: {
    display: "grid",
    gridTemplateColumns: "repeat(auto-fit, minmax(70px, 1fr))",
    gap: 6,
    alignItems: "start",
  },
  compactCard: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: 2,
    minWidth: 50,
  },
  compactLabel: {
    padding: "1px 4px",
    borderRadius: 999,
    background: "#18182e",
    color: "#7f7fa7",
    border: "1px solid #2a2a55",
    fontSize: 11,
    textTransform: "uppercase",
  },
  itemCard: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: 2,
    width: "100%",
    minWidth: 0,
  },
  itemCardDesired: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: 2,
    width: "100%",
    minWidth: 0,
    padding: "2px",
    borderRadius: 8,
    border: "1px solid rgba(245,200,66,0.6)",
    background: "rgba(245,200,66,0.15)",
  },
  itemText: {
    fontSize: 11,
    color: "#8e8eb6",
    textAlign: "center",
    lineHeight: 1.1,
    maxWidth: 84,
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
  itemDetail: {
    fontSize: 11,
    color: "#d8d8ea",
    textAlign: "center",
    lineHeight: 1.1,
  },
  badgeRow: {
    display: "flex",
    flexWrap: "wrap",
    gap: 2,
    justifyContent: "center",
  },
  badge: {
    padding: "1px 4px",
    borderRadius: 999,
    background: "#202043",
    color: "#c7c7ef",
    border: "1px solid #35356d",
    fontSize: 11,
    textTransform: "uppercase",
  },
  badgeAccent: {
    padding: "1px 4px",
    borderRadius: 999,
    background: "rgba(245,200,66,0.2)",
    color: "#f5c842",
    border: "1px solid rgba(245,200,66,0.35)",
    fontSize: 11,
    textTransform: "uppercase",
  },
  badgeMuted: {
    padding: "1px 4px",
    borderRadius: 999,
    background: "#18182e",
    color: "#7f7fa7",
    border: "1px solid #2a2a55",
    fontSize: 11,
    textTransform: "uppercase",
  },
  packChip: {
    fontSize: 13,
    color: "#5a5a88",
    background: "#131326",
    border: "1px solid #2a2a55",
    borderRadius: 4,
    padding: "2px 6px",
  },
  factCard: {
    display: "flex",
    flexDirection: "column",
    gap: 1,
    minWidth: 90,
    padding: "4px 6px",
    borderRadius: 6,
    background: "#131326",
    border: "1px solid #2a2a55",
  },
  factLabel: {
    fontSize: 11,
    color: "#7f7fa7",
    textTransform: "uppercase",
  },
  factValue: {
    fontSize: 13,
    color: "#ececff",
  },
};
