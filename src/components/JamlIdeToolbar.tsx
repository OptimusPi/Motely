"use client";

import React from "react";
import { JimboButton } from "../ui/panel.js";
import { JimboTabs } from "../ui/jimboTabs.js";
import { JimboColorOption } from "../ui/tokens.js";

export type JamlIdeMode = "visual" | "code" | "map" | "results" | "jamlyzer";

export interface JamlIdeToolbarProps {
  mode: JamlIdeMode;
  onModeChange: (mode: JamlIdeMode) => void;
  resultCount?: number;
  className?: string;
  onSearch?: () => void;
  isSearching?: boolean;
  onLoadFile?: () => void;
  isLoadingFile?: boolean;
}

export function JamlIdeToolbar({
  mode,
  onModeChange,
  resultCount = 0,
  className = "",
  onSearch,
  isSearching = false,
  onLoadFile,
  isLoadingFile = false,
}: JamlIdeToolbarProps) {
  const tabs = [
    { id: "visual", label: "Visual" },
    { id: "code", label: "JAML" },
    { id: "map", label: "Map" },
    { id: "results", label: resultCount > 0 ? `Results (${resultCount})` : "Results" },
    { id: "jamlyzer", label: "Jamlyzer" },
  ];

  return (
    <div
      className={className}
      style={{
        display: "flex",
        alignItems: "center",
        gap: 8,
        padding: "10px 10px 6px",
        borderBottom: `1px solid ${JimboColorOption.PANEL_EDGE}`,
        background: JimboColorOption.DARKEST,
        minWidth: 0,
      }}
    >
      <div style={{ flex: 1, minWidth: 0, paddingBottom: 3 }}>
        <JimboTabs
          tabs={tabs}
          activeTab={mode}
          onTabChange={(id) => onModeChange(id as JamlIdeMode)}
        />
      </div>

      {onSearch && (
        <div style={{ flexShrink: 0 }}>
          <JimboButton tone={isSearching ? "red" : "orange"} size="sm" onClick={onSearch}>
            {isSearching ? "Stop" : "Search"}
          </JimboButton>
        </div>
      )}

      {onLoadFile && (
        <div style={{ flexShrink: 0 }}>
          <JimboButton tone="blue" size="sm" onClick={onLoadFile} disabled={isLoadingFile}>
            {isLoadingFile ? "Loading..." : "Load File"}
          </JimboButton>
        </div>
      )}
    </div>
  );
}
