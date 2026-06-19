import React from "react";

/**
 * Game Card wrapper — bridges json-render to jaml-ui's sprite system.
 *
 * We lazily import the real JamlGameCard so json-render has no hard dependency
 * on jaml-ui's heavy sprite imports. The consumer must import this component
 * AND have jaml-ui available in their bundle.
 */

export interface JamlGameCardProps {
  type: "joker" | "consumable" | "playing";
  card: {
    name: string;
    edition?: string;
    seal?: string;
    isEternal?: boolean;
    isPerishable?: boolean;
    isRental?: boolean;
  };
  scale?: number;
  className?: string;
}

let JamlGameCardModule: any = null;

function loadModule() {
  if (!JamlGameCardModule) {
    try {
      // Dynamic import avoids top-level dependency on jaml-ui's sprite modules
      JamlGameCardModule = require("../../components/GameCard.js");
    } catch {
      console.error(
        "[json-render] JamlGameCard requires jaml-ui to be available. " +
          "Make sure 'jaml-ui' is installed and its peer deps (react, motely-wasm) are resolved."
      );
    }
  }
  return JamlGameCardModule;
}

export const JamlGameCard: React.FC<JamlGameCardProps> = ({
  type,
  card,
  scale = 1,
  className = "",
}) => {
  const mod = loadModule();
  if (!mod?.JamlGameCard) {
    return (
      <div
        className={className}
        style={{
          border: "2px dashed var(--j-panel-edge)",
          borderRadius: "var(--j-radius)",
          padding: "var(--j-space-4)",
          color: "var(--j-grey)",
          textAlign: "center" as const,
        }}
      >
        <span style={{ fontSize: "var(--j-text-sm)" }}>🃏 {card.name}</span>
        <br />
        <span style={{ fontSize: "var(--j-text-xs)", opacity: 0.6 }}>
          (JamlGameCard not available)
        </span>
      </div>
    );
  }

  const JGC = mod.JamlGameCard;
  return (
    <JGC
      type={type}
      card={card}
      scale={scale}
      className={className}
    />
  );
};
