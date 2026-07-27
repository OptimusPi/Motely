"use client";

import {
  MotelyBossBlind,
  MotelyVoucher,
  MotelyTag,
  MotelyBoosterPack,
  type MotelyJamlyzerAnteResult,
} from "motely-wasm";
import { JimboInnerPanel } from "../../ui/panel.js";
import { JimboText } from "../../ui/jimboText.js";
import { JimboBadge } from "../../ui/JimboBadge.js";
import { JimboStack, JimboRow } from "../../ui/JimboLayout.js";
import { JimboSpinner } from "../../ui/JimboSpinner.js";
import { BOSSES, VOUCHERS, TAGS, BOOSTER_PACKS } from "../../sprites/spriteData.js";
import { MOTELY_ITEM_FORMATS_BY_VALUE } from "../../decode/motelyItemFormats.js";
import {
  JamlBoss,
  JamlVoucher,
  JamlTag,
  JamlGameCard,
  resolveAnalyzerShopItem,
  type AnalyzerResolvedItem,
} from "../GameCard.js";

function getBossDisplayName(bossVal: MotelyBossBlind): string {
  const key = MotelyBossBlind[bossVal];
  if (!key) return "Small Blind";
  const normalizedKey = key.toLowerCase();
  const found = BOSSES.find((b) => b.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key;
}

function getVoucherDisplayName(voucherVal: MotelyVoucher): string {
  const key = MotelyVoucher[voucherVal];
  if (!key) return "";
  const normalizedKey = key.toLowerCase();
  const found = VOUCHERS.find((v) => v.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key;
}

function getTagDisplayName(tagVal: MotelyTag): string {
  const key = MotelyTag[tagVal];
  if (!key) return "";
  const normalizedKey = key.toLowerCase();
  const found = TAGS.find((t) => t.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key;
}

function getBoosterPackDisplayName(packVal: MotelyBoosterPack): string {
  const key = MotelyBoosterPack[packVal];
  if (!key) return "";
  const normalizedKey = (key + "pack").toLowerCase();
  const found = BOOSTER_PACKS.find((b) => b.name.replace(/[^a-zA-Z0-9]/g, "").toLowerCase() === normalizedKey);
  return found ? found.name : key + " Pack";
}

function getResolvedItem(value: number, scale = 0.5): AnalyzerResolvedItem {
  const format = MOTELY_ITEM_FORMATS_BY_VALUE[value as keyof typeof MOTELY_ITEM_FORMATS_BY_VALUE];
  if (format) {
    return resolveAnalyzerShopItem(
      {
        id: String(value),
        name: format.displayName,
        value: value,
      },
      scale
    );
  }
  return { kind: "unknown", label: `Unknown #${value}` };
}

export function ResolvedItem({ value, scale }: { value: number; scale: number }) {
  const resolved = getResolvedItem(value, scale);
  if (resolved.kind === "voucher") {
    return <JamlVoucher voucherName={resolved.voucherName} scale={scale} />;
  }
  if (resolved.kind === "joker" || resolved.kind === "consumable" || resolved.kind === "playing") {
    return <JamlGameCard card={resolved.card} type={resolved.type} />;
  }
  if (resolved.kind === "unknown") {
    return (
      <JimboBadge size="md" tone="grey" title={resolved.label}>
        ?
      </JimboBadge>
    );
  }
  return (
    <JimboBadge size="md" tone="grey" title="Unrecognized item">
      ?
    </JimboBadge>
  );
}

export interface JamlyzerAnteDetailsProps {
  ante: MotelyJamlyzerAnteResult | undefined;
  selectedAnte: number;
  minAnte: number;
  maxAnte: number;
  onSelectAnte: (ante: number) => void;
}

/** Spinner-driven single-ante detail: boss, voucher, tags, shop queue, packs. */
export function JamlyzerAnteDetails({
  ante,
  selectedAnte,
  minAnte,
  maxAnte,
  onSelectAnte,
}: JamlyzerAnteDetailsProps) {
  return (
    <JimboInnerPanel className="j-jamlyzer__details">
      <JimboSpinner
        value={`Ante ${selectedAnte}`}
        onPrev={() => onSelectAnte(Math.max(minAnte, selectedAnte - 1))}
        onNext={() => onSelectAnte(Math.min(maxAnte, selectedAnte + 1))}
        canPrev={selectedAnte > minAnte}
        canNext={selectedAnte < maxAnte}
      />

      {!ante ? (
        <JimboText size="xs" tone="grey" className="j-text-center">
          No analysis for Ante {selectedAnte}
        </JimboText>
      ) : (
        <JimboStack gap="sm" align="stretch">
          <JimboInnerPanel className="j-jamlyzer__details-section">
            <JimboText size="xs" tone="gold" className="j-text-center">
              Boss & voucher
            </JimboText>
            <JimboRow gap="md" justify="center" align="center">
              <JimboStack gap="xs" align="center">
                <JamlBoss bossName={getBossDisplayName(ante.boss)} scale={0.5} />
                <JimboText size="micro" className="j-text-center">
                  {getBossDisplayName(ante.boss)}
                </JimboText>
              </JimboStack>
              <JimboStack gap="xs" align="center">
                <JamlVoucher voucherName={getVoucherDisplayName(ante.voucher)} scale={0.5} />
                <JimboText size="micro" className="j-text-center">
                  {getVoucherDisplayName(ante.voucher)}
                </JimboText>
              </JimboStack>
            </JimboRow>
          </JimboInnerPanel>

          <JimboInnerPanel className="j-jamlyzer__details-section">
            <JimboText size="xs" tone="gold" className="j-text-center">
              Tags
            </JimboText>
            <JimboRow gap="md" justify="center" align="center">
              <JimboStack gap="xs" align="center">
                <JamlTag tagName={getTagDisplayName(ante.smallBlindTag)} scale={0.5} />
                <JimboText size="micro" className="j-text-center">
                  Small: {getTagDisplayName(ante.smallBlindTag)}
                </JimboText>
              </JimboStack>
              <JimboStack gap="xs" align="center">
                <JamlTag tagName={getTagDisplayName(ante.bigBlindTag)} scale={0.5} />
                <JimboText size="micro" className="j-text-center">
                  Big: {getTagDisplayName(ante.bigBlindTag)}
                </JimboText>
              </JimboStack>
            </JimboRow>
          </JimboInnerPanel>

          <JimboInnerPanel className="j-jamlyzer__details-section">
            <JimboText size="xs" tone="gold" className="j-text-center">
              Shop queue
            </JimboText>
            {ante.shopItems && ante.shopItems.length > 0 ? (
              <JimboRow wrap gap="xs" justify="center" align="start">
                {ante.shopItems.map((item, idx) => (
                  <ResolvedItem key={idx} value={item.value} scale={0.45} />
                ))}
              </JimboRow>
            ) : (
              <JimboText size="xs" tone="grey" className="j-text-center">
                Empty
              </JimboText>
            )}
          </JimboInnerPanel>

          <JimboInnerPanel className="j-jamlyzer__details-section">
            <JimboText size="xs" tone="gold" className="j-text-center">
              Booster packs
            </JimboText>
            {ante.packs && ante.packs.length > 0 ? (
              <JimboStack gap="sm" align="stretch">
                {ante.packs.map((pack, packIdx) => (
                  <JimboStack key={packIdx} gap="xs" align="center">
                    <JimboText size="xs" className="j-text-center">
                      {getBoosterPackDisplayName(pack.pack)}
                    </JimboText>
                    <JimboRow wrap gap="xs" justify="center" align="start">
                      {pack.items.map((item, itemIdx) => (
                        <ResolvedItem key={itemIdx} value={item.value} scale={0.45} />
                      ))}
                    </JimboRow>
                  </JimboStack>
                ))}
              </JimboStack>
            ) : (
              <JimboText size="xs" tone="grey" className="j-text-center">
                No packs opened
              </JimboText>
            )}
          </JimboInnerPanel>
        </JimboStack>
      )}
    </JimboInnerPanel>
  );
}
