"use client";

import type { MotelyJamlyzerSeedResult } from "motely-wasm";
import { JimboPanel } from "../../ui/JimboPanel.js";
import { JimboInnerPanel } from "../../ui/panel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboBadge } from "../../ui/JimboBadge.js";
import { JimboRow } from "../../ui/JimboLayout.js";

export interface JamlyzerEventsProps {
  events: MotelyJamlyzerSeedResult["events"];
}

const MAX_VISIBLE_ROLLS = 8;

/** Probabilistic event roll outcomes (Lucky cards, Cavendish, Wheel, …). */
export function JamlyzerEvents({ events }: JamlyzerEventsProps) {
  const rolls = [
    { key: "luckyMoney", label: "Lucky money", values: events.luckyMoney },
    { key: "luckyMult", label: "Lucky mult", values: events.luckyMult },
    { key: "cavendish", label: "Cavendish", values: events.cavendish },
    { key: "grosMichel", label: "Gros Michel", values: events.grosMichel },
    { key: "space", label: "Space Joker", values: events.space },
    { key: "business", label: "Business Card", values: events.business },
    { key: "bloodstone", label: "Bloodstone", values: events.bloodstone },
    { key: "parking", label: "Parking Meter", values: events.parking },
    { key: "eightBall", label: "Eight Ball", values: events.eightBall },
    { key: "glass", label: "Glass Joker", values: events.glass },
    { key: "omenGlobe", label: "Omen Globe", values: events.omenGlobe },
    { key: "theWheel", label: "The Wheel", values: events.theWheel },
  ].filter((r) => r.values && r.values.length > 0);

  if (rolls.length === 0) return null;

  return (
    <JimboPanel title="Event rolls" tone="gold">
      <JimboRow wrap gap="sm" align="start">
        {rolls.map((roll) => (
          <JimboInnerPanel key={roll.key} className="j-stack j-stack--gap-xs">
            <JimboText size="xs" tone="grey">
              {roll.label}
            </JimboText>
            <JimboRow wrap gap="xs" align="center">
              {roll.values.slice(0, MAX_VISIBLE_ROLLS).map((v, i) => (
                <JimboBadge key={i} tone="grey" size="sm">
                  {String(v)}
                </JimboBadge>
              ))}
              {roll.values.length > MAX_VISIBLE_ROLLS && (
                <JimboText size="micro" tone="grey">
                  +{roll.values.length - MAX_VISIBLE_ROLLS}
                </JimboText>
              )}
            </JimboRow>
          </JimboInnerPanel>
        ))}
      </JimboRow>
    </JimboPanel>
  );
}
