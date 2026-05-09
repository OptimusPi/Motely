"use client";
import React, { useState, useRef } from "react";
import { JimboPanel, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { JimboColorOption } from "../ui/tokens.js";

export interface JamlyzerProps {
  jaml: string;
  onTest: (seed: string) => void;
  result: "idle" | "match" | "nomatch" | "running" | "error";
  error?: string | null;
}

export function Jamlyzer({ jaml, onTest, result, error }: JamlyzerProps) {
  const [seed, setSeed] = useState("");
  const inputRef = useRef<HTMLInputElement>(null);

  const handleTest = () => {
    const s = seed.trim().toUpperCase();
    if (!s) return;
    onTest(s);
  };

  return (
    <div style={{ padding: 10, display: "flex", flexDirection: "column", gap: 8 }}>
      {/* Seed input */}
      <JimboPanel>
        <div style={{ display: "flex", gap: 6 }}>
          <input
            ref={inputRef}
            className="j-seed-input__field"
            type="text"
            placeholder="Enter seed..."
            value={seed}
            maxLength={12}
            onChange={(e) => setSeed(e.target.value.toUpperCase())}
            onKeyDown={(e) => e.key === "Enter" && handleTest()}
            style={{ flex: 1, fontSize: 15, padding: "6px 10px", letterSpacing: "0.1em", textTransform: "uppercase" }}
          />
          <JimboButton
            tone={result === "running" ? "red" : "orange"}
            size="sm"
            onClick={handleTest}
            disabled={!seed.trim() || !jaml.trim()}
          >
            {result === "running" ? "..." : "Test"}
          </JimboButton>
        </div>
      </JimboPanel>

      {/* Result */}
      {result === "match" && (
        <JimboPanel className="j-glow--match" style={{ background: `${JimboColorOption.GREEN_TEXT}22`, textAlign: "center" }}>
          <JimboText size="xl" tone="gold" style={{ letterSpacing: 3, display: "block", marginBottom: 4 }}>{seed}</JimboText>
          <JimboText size="md" tone="green">MATCH</JimboText>
        </JimboPanel>
      )}

      {result === "nomatch" && (
        <JimboPanel style={{ textAlign: "center" }}>
          <JimboText size="xl" tone="grey" style={{ letterSpacing: 3, display: "block", marginBottom: 4 }}>{seed}</JimboText>
          <JimboText size="md" tone="red">no match</JimboText>
        </JimboPanel>
      )}

      {result === "error" && (
        <JimboPanel>
          <JimboText size="xs" tone="red" style={{ display: "block", textAlign: "center" }}>{error ?? "Error"}</JimboText>
        </JimboPanel>
      )}

      {result === "idle" && !jaml.trim() && (
        <JimboPanel>
          <JimboText size="xs" tone="grey" style={{ display: "block", textAlign: "center" }}>
            Write a JAML filter in the JAML tab first
          </JimboText>
        </JimboPanel>
      )}
    </div>
  );
}
