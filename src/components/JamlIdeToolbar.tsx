"use client";

import React from "react";
import { JimboButton } from "../ui/panel.js";
import { JimboColorOption } from "../ui/tokens.js";

export type JamlIdeMode = "visual" | "code" | "map" | "results";

export interface JamlIdeToolbarProps {
  mode: JamlIdeMode;
  onModeChange: (mode: JamlIdeMode) => void;
  resultCount?: number;
  className?: string;
  onSearch?: () => void;
  isSearching?: boolean;
}

const TABS: Array<{ id: JamlIdeMode; label: string }> = [
  { id: "visual", label: "Visual" },
  { id: "code", label: ".jaml" },
  { id: "map", label: "Map" },
  { id: "results", label: "Results" },
];

export function JamlIdeToolbar({ mode, onModeChange, resultCount = 0, className = "", onSearch, isSearching = false }: JamlIdeToolbarProps) {
  return (
    <div
      className={className}
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        gap: 8,
        padding: "6px 10px",
        borderBottom: `1px solid ${JimboColorOption.PANEL_EDGE}`,
        background: JimboColorOption.DARKEST,
      }}
    >
      {/* Gold-fill segmented tab strip */}
      <div
        style={{
          display: "flex",
          alignItems: "stretch",
          gap: 0,
          background: `${JimboColorOption.DARK_GREY}cc`,
          borderRadius: 7,
          border: `1px solid ${JimboColorOption.PANEL_EDGE}`,
          padding: 2,
          overflow: "hidden",
        }}
      >
        {TABS.map((tab) => {
          const isActive = mode === tab.id;
          return (
            <button
              key={tab.id}
              type="button"
              onClick={() => onModeChange(tab.id)}
              style={{
                display: "flex",
                alignItems: "center",
                gap: 5,
                padding: "3px 10px",
                border: "none",
                borderRadius: 5,
                cursor: "pointer",
                fontFamily: "m6x11plus, monospace",
                fontSize: 10,
                letterSpacing: 1,
                textTransform: "uppercase",
                lineHeight: 1.2,
                transition: "background 80ms, color 80ms, box-shadow 80ms",
                color: isActive ? JimboColorOption.DARKEST : JimboColorOption.GREY,
                background: isActive ? JimboColorOption.GOLD : "transparent",
                boxShadow: isActive ? `0 2px 0 #8a6a1e` : "none",
                textShadow: isActive ? "none" : "none",
                userSelect: "none",
                position: "relative",
                transform: isActive ? "translateY(0)" : "translateY(0)",
              }}
            >
              {tab.label}
              {tab.id === "results" && resultCount > 0 ? (
                <span
                  style={{
                    borderRadius: 999,
                    background: isActive ? `${JimboColorOption.DARKEST}44` : `${JimboColorOption.GOLD}33`,
                    color: isActive ? JimboColorOption.DARKEST : JimboColorOption.GOLD_TEXT,
                    padding: "0px 5px",
                    fontSize: 9,
                    fontFamily: "monospace",
                    letterSpacing: 0,
                  }}
                >
                  {resultCount}
                </span>
              ) : null}
            </button>
          );
        })}
      </div>

      {onSearch ? (
        <JimboButton tone={isSearching ? "red" : "blue"} size="xs" onClick={onSearch}>
          {isSearching ? "Stop" : "Search"}
        </JimboButton>
      ) : null}
    </div>
  );
}
