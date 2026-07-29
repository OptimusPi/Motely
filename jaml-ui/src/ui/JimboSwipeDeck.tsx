"use client";

import type React from "react";
import { Children, useCallback, useEffect, useRef, useState, type ReactNode } from "react";
import { FiCheck, FiRotateCcw, FiX } from "react-icons/fi";
import { JimboIconButton } from "./JimboIconButton.js";
import { JimboText } from "./jimboText.js";

export type JimboSwipeDirection = "keep" | "pass";

export interface JimboSwipeDeckProps {
  /**
   * One card per child. Taken as children (not a render prop) so the deck can
   * live in the json-render registry — a spec can only carry data, and a
   * function prop would never survive the trip through JSON.
   */
  children?: ReactNode;
  /** Direct-React callers only; a spec cannot carry this. */
  onDecide?: (index: number, direction: JimboSwipeDirection) => void;
  width?: number;
  height?: number;
  /** Horizontal px before a release counts as a decision. */
  threshold?: number;
}

/** Cards drawn behind the top one. Deeper cards are occluded anyway. */
const STACK_DEPTH = 3;
const FLY_MS = 220;

interface Decision {
  index: number;
  direction: JimboSwipeDirection;
}

export function JimboSwipeDeck({
  children,
  onDecide,
  width = 300,
  height = 420,
  threshold = 90,
}: JimboSwipeDeckProps) {
  const cards = Children.toArray(children);

  const [cursor, setCursor] = useState(0);
  const [dx, setDx] = useState(0);
  const [dragging, setDragging] = useState(false);
  const [flying, setFlying] = useState<JimboSwipeDirection | null>(null);
  const [history, setHistory] = useState<Decision[]>([]);
  const startX = useRef(0);
  const flyTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  useEffect(
    () => () => {
      if (flyTimer.current) clearTimeout(flyTimer.current);
    },
    [],
  );

  const remaining = cards.length - cursor;

  const commit = useCallback(
    (direction: JimboSwipeDirection) => {
      if (flying || cursor >= cards.length) return;
      const index = cursor;
      setFlying(direction);
      setDragging(false);
      onDecide?.(index, direction);
      flyTimer.current = setTimeout(() => {
        setHistory((h) => [...h, { index, direction }]);
        setCursor(index + 1);
        setFlying(null);
        setDx(0);
      }, FLY_MS);
    },
    [cards.length, cursor, flying, onDecide],
  );

  const undo = useCallback(() => {
    if (flying || history.length === 0) return;
    const last = history[history.length - 1];
    setHistory((h) => h.slice(0, -1));
    setCursor(last.index);
    setDx(0);
  }, [flying, history]);

  const onPointerDown = (e: React.PointerEvent<HTMLDivElement>) => {
    if (flying) return;
    e.currentTarget.setPointerCapture(e.pointerId);
    startX.current = e.clientX;
    setDragging(true);
  };

  const onPointerMove = (e: React.PointerEvent<HTMLDivElement>) => {
    if (!dragging || flying) return;
    setDx(e.clientX - startX.current);
  };

  const onPointerUp = () => {
    if (!dragging || flying) return;
    setDragging(false);
    if (Math.abs(dx) >= threshold) commit(dx > 0 ? "keep" : "pass");
    else setDx(0);
  };

  const onKeyDown = (e: React.KeyboardEvent<HTMLDivElement>) => {
    if (e.key === "ArrowRight") commit("keep");
    if (e.key === "ArrowLeft") commit("pass");
    if (e.key === "Backspace") undo();
  };

  // Drag offset while held; launch vector once a decision commits.
  const offset = flying ? (flying === "keep" ? width * 2 : -width * 2) : dx;
  const tilt = Math.max(-14, Math.min(14, offset * 0.05));
  const verdict = Math.abs(dx) >= threshold ? (dx > 0 ? "keep" : "pass") : null;

  return (
    <div
      style={{
        display: "grid",
        gridTemplateRows: `${height}px auto auto`,
        gap: 14,
        justifyItems: "center",
      }}
    >
      <div
        role="group"
        aria-label="Seed triage deck"
        tabIndex={0}
        onKeyDown={onKeyDown}
        style={{
          position: "relative",
          width,
          height,
          touchAction: "none",
          outline: "none",
        }}
      >
        {remaining === 0 ? (
          <div style={{ position: "absolute", inset: 0, display: "grid", placeItems: "center" }}>
            <JimboText size="sm" tone="grey">
              No seeds left
            </JimboText>
          </div>
        ) : null}

        {cards.slice(cursor, cursor + STACK_DEPTH).map((card, depth) => {
          const isTop = depth === 0;
          const transform = isTop
            ? `translate3d(${offset}px, 0, 0) rotate(${tilt}deg)`
            : `translate3d(0, ${depth * 9}px, 0) scale(${1 - depth * 0.045})`;

          return (
            <div
              key={cursor + depth}
              onPointerDown={isTop ? onPointerDown : undefined}
              onPointerMove={isTop ? onPointerMove : undefined}
              onPointerUp={isTop ? onPointerUp : undefined}
              onPointerCancel={isTop ? onPointerUp : undefined}
              style={{
                position: "absolute",
                inset: 0,
                zIndex: STACK_DEPTH - depth,
                transform,
                transition:
                  dragging && isTop
                    ? "none"
                    : `transform ${FLY_MS}ms ease-out, opacity ${FLY_MS}ms ease-out`,
                opacity: isTop && flying ? 0 : 1,
                cursor: isTop ? (dragging ? "grabbing" : "grab") : "default",
                overflow: "hidden",
              }}
            >
              {card}

              {isTop && verdict ? (
                <div
                  style={{
                    position: "absolute",
                    top: 14,
                    left: verdict === "keep" ? 14 : undefined,
                    right: verdict === "pass" ? 14 : undefined,
                    padding: "4px 10px",
                    borderRadius: 4,
                    border: `2px solid ${verdict === "keep" ? "#3fa34d" : "#c0392b"}`,
                    transform: `rotate(${verdict === "keep" ? -8 : 8}deg)`,
                    pointerEvents: "none",
                  }}
                >
                  <JimboText size="xs" tone={verdict === "keep" ? "green" : "red"}>
                    {verdict === "keep" ? "Keep" : "Pass"}
                  </JimboText>
                </div>
              ) : null}
            </div>
          );
        })}
      </div>

      <div style={{ display: "grid", gridAutoFlow: "column", gap: 16, alignItems: "center" }}>
        <JimboIconButton
          aria-label="Pass"
          title="Pass (left arrow)"
          tone="destructive"
          onClick={() => commit("pass")}
          disabled={remaining === 0}
        >
          <FiX />
        </JimboIconButton>

        <JimboIconButton
          aria-label="Undo"
          title="Undo (backspace)"
          size="sm"
          onClick={undo}
          disabled={history.length === 0}
        >
          <FiRotateCcw />
        </JimboIconButton>

        <JimboIconButton
          aria-label="Keep"
          title="Keep (right arrow)"
          onClick={() => commit("keep")}
          disabled={remaining === 0}
        >
          <FiCheck />
        </JimboIconButton>
      </div>

      <JimboText size="micro" tone="grey">
        {remaining} of {cards.length} left
      </JimboText>
    </div>
  );
}
