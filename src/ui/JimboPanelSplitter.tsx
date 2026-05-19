"use client";

import { useCallback, useEffect, useRef, useLayoutEffect } from "react";

export interface JimboPanelSplitterProps {
  "aria-label"?: string;
  className?: string;
  onDrag: (delta: number) => void;
  onKeyAdjust?: (delta: number) => void;
  orientation?: "vertical" | "horizontal";
}

export function JimboPanelSplitter({
  orientation = "vertical",
  onDrag,
  onKeyAdjust,
  className = "",
  "aria-label": ariaLabel,
}: JimboPanelSplitterProps) {
  const draggingRef = useRef(false);
  const lastRef = useRef(0);
  const onDragRef = useRef(onDrag);
  useLayoutEffect(() => {
    onDragRef.current = onDrag;
  });

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
      className={`j-panel-splitter ${className}`.trim()}
      data-orientation={orientation}
      type="button"
    />
  );
}
