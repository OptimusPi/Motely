"use client";

import React from "react";
import { JimboButton } from "../ui/panel.js";
import { JimboTabs } from "../ui/jimboTabs.js";
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

export function JamlIdeToolbar({ mode, onModeChange, resultCount = 0, className = "", onSearch, isSearching = false }: JamlIdeToolbarProps) {
  const tabs = [
    { id: "visual", label: "Visual" },
    { id: "code", label: ".jaml" },
    { id: "map", label: "Map" },
    { id: "results", label: resultCount > 0 ? `Results (${resultCount})` : "Results" },
  ];

  return (
    <div
      className={className}
      style={{
        display: "grid",
        gridTemplateColumns: onSearch ? "1fr auto 1fr" : "1fr",
        alignItems: "center",
        gap: 8,
        minWidth: 0,
        padding: "10px 10px 6px",
        borderBottom: `1px solid ${JimboColorOption.PANEL_EDGE}`,
        background: JimboColorOption.DARKEST,
      }}
    >
      {onSearch ? <div /> : null}

      <div style={{ minWidth: 0, overflow: "hidden", width: "100%" }}>
        <JimboTabs
          tabs={tabs}
          activeTab={mode}
          onTabChange={(id) => onModeChange(id as JamlIdeMode)}
        />
      </div>

      {onSearch ? (
        <div style={{ justifySelf: "end" }}>
          <JimboButton tone={isSearching ? "red" : "blue"} size="xs" onClick={onSearch}>
            {isSearching ? "Stop" : "Search"}
          </JimboButton>
        </div>
      ) : null}
    </div>
  );
}
