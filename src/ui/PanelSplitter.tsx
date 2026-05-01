// PORTABLE — intended for jaml-ui/src/ui/panelSplitter.tsx
// On paste, replace `from 'jaml-ui'` with `from './tokens.js'`.
"use client";

import { JimboColorOption } from "./tokens.js";
import { useCallback, useEffect, useRef } from "react";

const C = JimboColorOption;

export interface PanelSplitterProps {
  "aria-label"?: string;
  onDrag: (delta: number) => void;
  onKeyAdjust?: (delta: number) => void;
  orientation?: "vertical" | "horizontal";
}

export function PanelSplitter({
  orientation = "vertical",
  onDrag,
  onKeyAdjust,
  "aria-label": ariaLabel,
}: PanelSplitterProps) {
  const draggingRef = useRef(false);
  const lastRef = useRef(0);
  const onDragRef = useRef(onDrag);
  onDragRef.current = onDrag;

  const handlePointerDown = useCallback(
    (e: React.PointerEvent<HTMLElement>) => {
      e.preventDefault();
      (e.target as HTMLElement).setPointerCapture(e.pointerId);
      draggingRef.current = true;
      lastRef.current = orientation === "vertical" ? e.clientX : e.clientY;
    },
    [orientation]
  );

  useEffect(() => {
    function move(e: PointerEvent) {
      if (!draggingRef.current) {
        return;
      }
      const cur = orientation === "vertical" ? e.clientX : e.clientY;
      const delta = cur - lastRef.current;
      if (delta !== 0) {
        lastRef.current = cur;
        onDragRef.current(delta);
      }
    }
    function up() {
      draggingRef.current = false;
    }
    window.addEventListener("pointermove", move);
    window.addEventListener("pointerup", up);
    window.addEventListener("pointercancel", up);
    return () => {
      window.removeEventListener("pointermove", move);
      window.removeEventListener("pointerup", up);
      window.removeEventListener("pointercancel", up);
    };
  }, [orientation]);

  const step = 16;
  const isVertical = orientation === "vertical";

  return (
    <button
      aria-label={ariaLabel ?? "Resize panel"}
      onKeyDown={(e) => {
        if (!onKeyAdjust) {
          return;
        }
        if (isVertical && e.key === "ArrowLeft") {
          onKeyAdjust(-step);
        }
        if (isVertical && e.key === "ArrowRight") {
          onKeyAdjust(step);
        }
        if (!isVertical && e.key === "ArrowUp") {
          onKeyAdjust(-step);
        }
        if (!isVertical && e.key === "ArrowDown") {
          onKeyAdjust(step);
        }
      }}
      onPointerDown={handlePointerDown}
      style={{
        all: "unset",
        display: "block",
        flex: "0 0 auto",
        width: isVertical ? 6 : undefined,
        height: isVertical ? undefined : 6,
        cursor: isVertical ? "col-resize" : "row-resize",
        background: C.PANEL_EDGE,
        borderLeft: isVertical ? `1px solid ${C.BLACK}` : undefined,
        borderRight: isVertical ? `1px solid ${C.BLACK}` : undefined,
        borderTop: isVertical ? undefined : `1px solid ${C.BLACK}`,
        borderBottom: isVertical ? undefined : `1px solid ${C.BLACK}`,
        touchAction: "none",
        userSelect: "none",
      }}
      type="button"
    />
  );
}
