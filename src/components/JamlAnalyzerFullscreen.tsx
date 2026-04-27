"use client";

import React, { useCallback, useEffect, useMemo, useRef, useState } from "react";
import { JamlBoss, JamlGameCard, JamlTag, JamlVoucher, resolveAnalyzerShopItem } from "./GameCard.js";
import { useMotelyStream, type StreamItem } from "../hooks/useShopStream.js";
import type { AnalyzerAnteView, AnalyzerItem } from "./AnalyzerExplorer.js";
import type { AnalyzerLive } from "../hooks/useAnalyzer.js";
import {
  ANALYZER_STREAM_META,
  DEFAULT_ENABLED_STREAMS,
  buildStreamHandle,
  type AnalyzerStreamKey,
} from "../hooks/analyzerStreamRegistry.js";
import { JimboColorOption, withAlpha } from "../ui/tokens.js";

const C = JimboColorOption;

const TONE_COLORS: Record<string, string> = {
  gold: C.GOLD_TEXT,
  purple: C.TAROT_BUTTON,
  blue: C.PLANET_BUTTON,
  spectral: C.SPECTRAL_BUTTON,
  default: C.GOLD_TEXT,
};

export interface JamlAnalyzerFullscreenProps {
  /** Per-ante summaries from useAnalyzer.antes. */
  antes: AnalyzerAnteView[];
  /** Live ctx from useAnalyzer.live; null disables additional stream lanes. */
  live: AnalyzerLive | null;
  /** Stream lanes to surface. Defaults to shop + soul jokers. */
  enabledStreams?: AnalyzerStreamKey[];
  /** Called when the user toggles a stream in the picker. Owners persist if desired. */
  onEnabledStreamsChange?: (next: AnalyzerStreamKey[]) => void;
  /** Hide the built-in stream picker overlay (e.g. when host renders its own). */
  hidePicker?: boolean;
  /** Pull size on each lazy load. */
  chunkSize?: number;
  className?: string;
}

export function JamlAnalyzerFullscreen({
  antes,
  live,
  enabledStreams,
  onEnabledStreamsChange,
  hidePicker = false,
  chunkSize = 12,
  className = "",
}: JamlAnalyzerFullscreenProps) {
  const [internalEnabled, setInternalEnabled] = useState<AnalyzerStreamKey[]>(
    enabledStreams ?? DEFAULT_ENABLED_STREAMS,
  );
  const effectiveEnabled = enabledStreams ?? internalEnabled;

  const setEnabled = useCallback(
    (next: AnalyzerStreamKey[]) => {
      setInternalEnabled(next);
      onEnabledStreamsChange?.(next);
    },
    [onEnabledStreamsChange],
  );

  const scrollRef = useRef<HTMLDivElement>(null);
  const sectionRefs = useRef<Map<number, HTMLElement>>(new Map());
  const [currentAnte, setCurrentAnte] = useState<number>(antes[0]?.ante ?? 1);
  const [pickerOpen, setPickerOpen] = useState(false);

  useEffect(() => {
    const root = scrollRef.current;
    if (!root) return;
    const observer = new IntersectionObserver(
      (entries) => {
        const top = entries
          .filter((e) => e.isIntersecting)
          .sort((a, b) => b.intersectionRatio - a.intersectionRatio)[0];
        if (!top) return;
        const ante = Number((top.target as HTMLElement).dataset.ante);
        if (!Number.isNaN(ante)) setCurrentAnte(ante);
      },
      { root, threshold: [0.4, 0.6, 0.8] },
    );
    sectionRefs.current.forEach((el) => observer.observe(el));
    return () => observer.disconnect();
  }, [antes]);

  const scrollToAnte = useCallback((ante: number) => {
    sectionRefs.current.get(ante)?.scrollIntoView({ behavior: "smooth", block: "start" });
  }, []);

  return (
    <div className={className} style={styles.root}>
      <div ref={scrollRef} style={styles.scroller}>
        {antes.map((ante) => (
          <AnteSection
            key={ante.ante}
            ante={ante}
            live={live}
            enabledStreams={effectiveEnabled}
            chunkSize={chunkSize}
            registerRef={(el) => {
              if (el) sectionRefs.current.set(ante.ante, el);
              else sectionRefs.current.delete(ante.ante);
            }}
          />
        ))}
      </div>

      <SideRail
        antes={antes.map((a) => a.ante)}
        currentAnte={currentAnte}
        onJump={scrollToAnte}
      />

      {!hidePicker && (
        <StreamPicker
          enabled={effectiveEnabled}
          onChange={setEnabled}
          open={pickerOpen}
          onToggle={() => setPickerOpen((v) => !v)}
        />
      )}
    </div>
  );
}

interface AnteSectionProps {
  ante: AnalyzerAnteView;
  live: AnalyzerLive | null;
  enabledStreams: AnalyzerStreamKey[];
  chunkSize: number;
  registerRef: (el: HTMLElement | null) => void;
}

function AnteSection({ ante, live, enabledStreams, chunkSize, registerRef }: AnteSectionProps) {
  return (
    <section
      ref={registerRef as React.RefCallback<HTMLElement>}
      data-ante={ante.ante}
      style={styles.section}
    >
      <header style={styles.header}>
        <div>
          <div style={styles.anteLabel}>Ante</div>
          <div style={styles.anteNumber}>{ante.ante}</div>
        </div>
        {ante.voucher && (
          <div style={styles.voucherBlock}>
            <JamlVoucher voucherName={ante.voucher} scale={0.85} />
            <div style={styles.voucherCaption}>{ante.voucher}</div>
          </div>
        )}
      </header>

      <div style={styles.blindRow}>
        <BlindCell label="Small" tag={ante.smallBlindTag} />
        <BlindCell label="Big" tag={ante.bigBlindTag} />
        {ante.boss && (
          <div style={styles.bossCell}>
            <div style={styles.cellLabel}>Boss</div>
            <JamlBoss bossName={ante.boss} scale={0.7} />
            <div style={styles.cellCaption}>{ante.boss}</div>
          </div>
        )}
      </div>

      {ante.packs && ante.packs.length > 0 && (
        <div style={styles.streamLane}>
          <div style={styles.streamLabel}>Packs</div>
          <div style={styles.packRow}>
            {ante.packs.map((pack, i) => (
              <div key={`${ante.ante}-pack-${i}`} style={styles.packPill}>
                {pack}
              </div>
            ))}
          </div>
        </div>
      )}

      {enabledStreams.map((key) => {
        const isShop = key === "shop";
        const initialItems: StreamItem[] = isShop
          ? (ante.shop ?? []).map((item: AnalyzerItem) => ({
              id: item.id,
              name: item.name,
              value: item.value,
            }))
          : [];
        return (
          <StreamLane
            key={`${ante.ante}-${key}`}
            ante={ante.ante}
            streamKey={key}
            live={live}
            chunkSize={chunkSize}
            initialItems={initialItems}
          />
        );
      })}
    </section>
  );
}

interface StreamLaneProps {
  ante: number;
  streamKey: AnalyzerStreamKey;
  live: AnalyzerLive | null;
  chunkSize: number;
  initialItems: StreamItem[];
}

function StreamLane({ ante, streamKey, live, chunkSize, initialItems }: StreamLaneProps) {
  const meta = ANALYZER_STREAM_META[streamKey];
  const handle = useMemo(
    () => (live ? buildStreamHandle(live, ante, streamKey) : null),
    [live, ante, streamKey],
  );
  const stream = useMotelyStream(
    handle?.initStream ?? null,
    handle?.nextItem ?? null,
    [ante, streamKey, live?.seed, live?.deck, live?.stake],
    initialItems,
  );
  const desired = live?.desiredNames ?? new Set<string>();
  const toneColor = TONE_COLORS[meta.tone] ?? TONE_COLORS.default;

  return (
    <div style={styles.streamLane}>
      <div style={{ ...styles.streamLabel, color: toneColor }}>
        {meta.label}
        {stream.items.length > 0 ? ` · ${stream.items.length}` : ""}
      </div>
      <ShopRow
        items={stream.items}
        desired={desired}
        loadingMore={stream.loadingMore}
        ready={stream.ready}
        onPullMore={() => stream.pullMore(chunkSize)}
      />
      {stream.error && <div style={styles.errorLine}>stream error: {stream.error}</div>}
    </div>
  );
}

function BlindCell({ label, tag }: { label: string; tag?: string }) {
  if (!tag) return null;
  return (
    <div style={styles.blindCell}>
      <div style={styles.cellLabel}>{label}</div>
      <JamlTag tagName={tag} scale={0.7} />
      <div style={styles.cellCaption}>{tag}</div>
    </div>
  );
}

interface ShopRowProps {
  items: StreamItem[];
  desired: ReadonlySet<string>;
  loadingMore: boolean;
  ready: boolean;
  onPullMore: () => void;
}

function ShopRow({ items, desired, loadingMore, ready, onPullMore }: ShopRowProps) {
  const sentinelRef = useRef<HTMLDivElement>(null);
  const lastTriggerRef = useRef(0);

  useEffect(() => {
    const el = sentinelRef.current;
    if (!el || !ready) return;
    const observer = new IntersectionObserver(
      (entries) => {
        if (entries[0]?.isIntersecting && !loadingMore) {
          const now = Date.now();
          if (now - lastTriggerRef.current < 200) return;
          lastTriggerRef.current = now;
          onPullMore();
        }
      },
      { root: el.parentElement, threshold: 0.1, rootMargin: "0px 200px 0px 0px" },
    );
    observer.observe(el);
    return () => observer.disconnect();
  }, [ready, loadingMore, onPullMore]);

  return (
    <div style={styles.shopRow}>
      {items.map((item) => (
        <ShopItem key={item.id} item={item} desired={desired.has(item.name.toLowerCase())} />
      ))}
      <div ref={sentinelRef} style={styles.sentinel}>
        {loadingMore ? "…" : ""}
      </div>
    </div>
  );
}

function ShopItem({ item, desired }: { item: StreamItem; desired: boolean }) {
  const resolved = resolveAnalyzerShopItem({ id: item.id, name: item.name, value: item.value });
  const wrapperStyle: React.CSSProperties = {
    flexShrink: 0,
    position: "relative",
    filter: desired ? `drop-shadow(0 0 6px ${C.GOLD})` : undefined,
  };
  const labelStyle: React.CSSProperties = {
    fontSize: 10,
    color: desired ? C.GOLD : C.GREY,
    textAlign: "center",
    marginTop: 2,
    maxWidth: 80,
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  };
  return (
    <div style={wrapperStyle}>
      {resolved.kind === "joker" || resolved.kind === "consumable" || resolved.kind === "playing" ? (
        <JamlGameCard card={resolved.card} type={resolved.type} />
      ) : resolved.kind === "voucher" ? (
        <JamlVoucher voucherName={resolved.voucherName} scale={0.7} />
      ) : (
        <div style={styles.unknownTile}>{("label" in resolved ? resolved.label : item.name) || "?"}</div>
      )}
      <div style={labelStyle}>{item.name}</div>
    </div>
  );
}

interface SideRailProps {
  antes: number[];
  currentAnte: number;
  onJump: (ante: number) => void;
}

function SideRail({ antes, currentAnte, onJump }: SideRailProps) {
  return (
    <div style={styles.sideRail}>
      {antes.map((ante) => {
        const active = ante === currentAnte;
        return (
          <button
            key={ante}
            type="button"
            onClick={() => onJump(ante)}
            aria-label={`Jump to ante ${ante}`}
            style={{
              ...styles.sideRailDot,
              background: active ? C.GOLD : withAlpha(C.WHITE, 0.25),
              transform: active ? "scale(1.4)" : "scale(1)",
              boxShadow: active ? `0 0 8px ${C.GOLD}` : "none",
            }}
          />
        );
      })}
    </div>
  );
}

interface StreamPickerProps {
  enabled: AnalyzerStreamKey[];
  onChange: (next: AnalyzerStreamKey[]) => void;
  open: boolean;
  onToggle: () => void;
}

function StreamPicker({ enabled, onChange, open, onToggle }: StreamPickerProps) {
  const enabledSet = new Set(enabled);
  const all = Object.values(ANALYZER_STREAM_META);

  function toggle(key: AnalyzerStreamKey) {
    const next = new Set(enabledSet);
    if (next.has(key)) next.delete(key);
    else next.add(key);
    onChange(all.map((m) => m.key).filter((k) => next.has(k)));
  }

  return (
    <>
      <button
        type="button"
        onClick={onToggle}
        style={styles.pickerButton}
        aria-label="Toggle stream picker"
      >
        {open ? "✕" : "≡"}
      </button>
      {open && (
        <div style={styles.pickerPanel}>
          <div style={styles.pickerHeader}>Streams</div>
          {all.map((meta) => {
            const isOn = enabledSet.has(meta.key);
            const tone = TONE_COLORS[meta.tone] ?? TONE_COLORS.default;
            return (
              <button
                key={meta.key}
                type="button"
                onClick={() => toggle(meta.key)}
                style={{
                  ...styles.pickerChip,
                  borderColor: isOn ? tone : withAlpha(C.WHITE, 0.15),
                  color: isOn ? tone : C.GREY,
                  background: isOn ? withAlpha(tone, 0.1) : "transparent",
                }}
              >
                {isOn ? "●" : "○"} {meta.label}
              </button>
            );
          })}
        </div>
      )}
    </>
  );
}

const styles: Record<string, React.CSSProperties> = {
  root: {
    position: "relative",
    width: "100%",
    height: "100svh",
    background: C.DARKEST,
    color: C.WHITE,
    fontFamily: "var(--font-sans, m6x11plus), monospace",
    overflow: "hidden",
  },
  scroller: {
    width: "100%",
    height: "100%",
    overflowY: "scroll",
    scrollSnapType: "y mandatory",
    scrollBehavior: "smooth",
    WebkitOverflowScrolling: "touch",
  },
  section: {
    width: "100%",
    minHeight: "100svh",
    scrollSnapAlign: "start",
    padding: "20px 16px 28px",
    display: "flex",
    flexDirection: "column",
    gap: 12,
    boxSizing: "border-box",
    borderBottom: `1px solid ${withAlpha(C.WHITE, 0.05)}`,
  },
  header: {
    display: "flex",
    justifyContent: "space-between",
    alignItems: "flex-start",
    gap: 12,
  },
  anteLabel: {
    fontSize: 12,
    color: C.GREY,
    letterSpacing: "0.16em",
  },
  anteNumber: {
    fontSize: 72,
    color: C.GOLD,
    lineHeight: 0.9,
    textShadow: `3px 3px 0 ${C.BLACK}`,
  },
  voucherBlock: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: 4,
  },
  voucherCaption: {
    fontSize: 10,
    color: C.PURPLE,
    letterSpacing: "0.1em",
  },
  blindRow: {
    display: "flex",
    gap: 16,
    flexWrap: "wrap",
  },
  blindCell: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: 4,
  },
  bossCell: {
    display: "flex",
    flexDirection: "column",
    alignItems: "center",
    gap: 4,
  },
  cellLabel: {
    fontSize: 10,
    color: C.GREY,
    letterSpacing: "0.12em",
  },
  cellCaption: {
    fontSize: 10,
    color: C.WHITE,
    maxWidth: 90,
    textAlign: "center",
    overflow: "hidden",
    textOverflow: "ellipsis",
    whiteSpace: "nowrap",
  },
  streamLane: {
    display: "flex",
    flexDirection: "column",
    gap: 6,
    minHeight: 0,
  },
  streamLabel: {
    fontSize: 11,
    color: C.GOLD_TEXT,
    letterSpacing: "0.16em",
  },
  shopRow: {
    display: "flex",
    gap: 10,
    overflowX: "auto",
    overflowY: "hidden",
    paddingBottom: 6,
    scrollbarWidth: "thin",
  },
  packRow: {
    display: "flex",
    flexWrap: "wrap",
    gap: 6,
  },
  packPill: {
    padding: "4px 10px",
    background: withAlpha(C.PANEL_EDGE, 0.9),
    border: `1px solid ${C.INNER_BORDER}`,
    borderRadius: 4,
    fontSize: 11,
    color: C.WHITE,
  },
  sentinel: {
    flexShrink: 0,
    width: 28,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    color: C.GREY,
    fontSize: 14,
  },
  unknownTile: {
    width: 71,
    height: 95,
    border: `1px dashed ${C.GREY}`,
    borderRadius: 4,
    display: "flex",
    alignItems: "center",
    justifyContent: "center",
    fontSize: 9,
    color: C.GREY,
    padding: 4,
    textAlign: "center",
  },
  errorLine: {
    fontSize: 10,
    color: C.RED,
  },
  sideRail: {
    position: "absolute",
    top: "50%",
    right: 6,
    transform: "translateY(-50%)",
    display: "flex",
    flexDirection: "column",
    gap: 8,
    zIndex: 5,
    pointerEvents: "auto",
  },
  sideRailDot: {
    width: 8,
    height: 8,
    borderRadius: "50%",
    border: "none",
    cursor: "pointer",
    padding: 0,
    transition: "transform 0.15s ease, background 0.15s ease, box-shadow 0.15s ease",
  },
  pickerButton: {
    position: "absolute",
    top: 12,
    right: 12,
    width: 32,
    height: 32,
    borderRadius: 4,
    border: `1px solid ${withAlpha(C.WHITE, 0.2)}`,
    background: withAlpha(C.DARK_GREY, 0.85),
    color: C.WHITE,
    fontSize: 16,
    cursor: "pointer",
    zIndex: 6,
    fontFamily: "inherit",
  },
  pickerPanel: {
    position: "absolute",
    top: 50,
    right: 12,
    width: 220,
    maxHeight: "70vh",
    overflowY: "auto",
    padding: 10,
    background: withAlpha(C.DARK_GREY, 0.95),
    border: `1px solid ${withAlpha(C.WHITE, 0.15)}`,
    borderRadius: 6,
    display: "flex",
    flexDirection: "column",
    gap: 4,
    zIndex: 6,
    backdropFilter: "blur(4px)",
  },
  pickerHeader: {
    fontSize: 10,
    color: C.GREY,
    letterSpacing: "0.16em",
    marginBottom: 4,
  },
  pickerChip: {
    padding: "6px 10px",
    border: "1px solid",
    borderRadius: 4,
    fontSize: 11,
    fontFamily: "inherit",
    cursor: "pointer",
    textAlign: "left",
    transition: "all 0.12s ease",
  },
};

export type { AnalyzerItem };
export { ANALYZER_STREAM_META, type AnalyzerStreamKey } from "../hooks/analyzerStreamRegistry.js";
