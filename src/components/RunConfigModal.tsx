import React, { useState } from "react";
import { JimboModal } from "../ui/panel.js";
import { DECK_OPTIONS, STAKE_OPTIONS } from "../lib/data/constants.js";
import { DeckSprite } from "./DeckSprite.js";
import { StakeSprite } from "../ui/sprites.js";
import { JimboColorOption } from "../ui/tokens.js";

const DECK_DESCRIPTIONS: Record<string, string> = {
  "Red": "+1 discard every round",
  "Blue": "+1 hand every round",
  "Yellow": "Start with extra $10",
  "Green": "At end of each Round:\n$2 per remaining Hand,\n$1 per remaining Discard.\nEarn no Interest",
  "Black": "+1 Joker slot\n-1 hand every round",
  "Magic": "Start run with the\nCrystal Ball voucher\nand 2 copies of The Fool",
  "Nebula": "Start run with the\nTelescope voucher\n-1 consumable slot",
  "Ghost": "Spectral cards may\nappear in the shop,\nstart with a Hex card",
  "Abandoned": "Start run with no\nFace Cards in your deck",
  "Checkered": "Start run with\n26 Spades and\n26 Hearts in deck",
  "Zodiac": "Start run with\nTarot Merchant,\nPlanet Merchant,\nand Overstock vouchers",
  "Painted": "+2 hand size,\n-1 Joker slot",
  "Anaglyph": "After defeating each\nBoss Blind, gain a\nDouble Tag",
  "Plasma": "Balance Chips and\nMult when calculating\nscore for played hand.\nX2 base Blind size",
  "Erratic": "All Ranks and Suits\nin deck are randomized",
};

const STAKE_DESCRIPTIONS: Record<string, string> = {
  "White": "Base Difficulty",
  "Red": "Small Blind gives\nno reward money\nApplies all previous Stakes",
  "Green": "Required score scales\nfaster for each Ante\nApplies all previous Stakes",
  "Black": "Shop can have Jokers\nwith Eternal\nApplies all previous Stakes",
  "Blue": "-1 Discard\nApplies all previous Stakes",
  "Purple": "Required score scales\nfaster for each Ante\nApplies all previous Stakes",
  "Orange": "Shop can have Jokers\nwith Perishable\nApplies all previous Stakes",
  "Gold": "-1 hand size\nShop can have Jokers\nwith Rental\nApplies all previous Stakes",
};

function formatTextWithColors(text: string) {
  // Hacky basic coloration for Balatro text
  const parts = text.split(/(\d+|Spades|Hearts|Clubs|Diamonds|\$[\d]+|Eternal|Perishable|Rental|Chips|Mult)/g);
  return parts.map((part, i) => {
    let color: string = JimboColorOption.WHITE;
    if (/^\d+$/.test(part)) color = JimboColorOption.RED;
    else if (part === 'Spades') color = JimboColorOption.BLUE; // Spades are typically blue in high contrast
    else if (part === 'Hearts') color = JimboColorOption.RED;
    else if (part === 'Clubs') color = JimboColorOption.GREEN_TEXT;
    else if (part === 'Diamonds') color = JimboColorOption.ORANGE_TEXT;
    else if (part.startsWith('$')) color = JimboColorOption.GOLD_TEXT;
    else if (['Eternal', 'Perishable', 'Rental'].includes(part)) color = JimboColorOption.GOLD_TEXT; // Should be specific colours
    else if (part === 'Chips') color = JimboColorOption.BLUE;
    else if (part === 'Mult') color = JimboColorOption.RED;

    if (color !== JimboColorOption.WHITE) {
      return <span key={i} style={{ color }}>{part}</span>;
    }
    return <span key={i}>{part}</span>;
  });
}

function CarouselButton({ onClick, children }: { onClick: () => void, children: React.ReactNode }) {
  return (
    <button
      onClick={onClick}
      style={{
        width: 32,
        height: "100%",
        borderRadius: 8,
        background: JimboColorOption.RED,
        border: `2px solid ${JimboColorOption.WHITE}`,
        color: JimboColorOption.WHITE,
        fontSize: 24,
        fontFamily: "m6x11plus, monospace",
        display: "flex",
        alignItems: "center",
        justifyContent: "center",
        cursor: "pointer",
        flexShrink: 0,
      }}
      className="j-btn j-btn--red"
    >
      <div className="j-btn__face" style={{ padding: 0, display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
        {children}
      </div>
    </button>
  );
}

export interface RunConfigModalProps {
  open: boolean;
  onClose: () => void;
  deck: string;
  stake: string;
  onChange: (deck: string, stake: string) => void;
}

export function RunConfigModal({
  open,
  onClose,
  deck,
  stake,
  onChange,
}: RunConfigModalProps) {
  const [activeDeck, setActiveDeck] = useState(deck);
  const [activeStake, setActiveStake] = useState(stake);

  const [prevProps, setPrevProps] = useState({ open, deck, stake });
  if (open !== prevProps.open || deck !== prevProps.deck || stake !== prevProps.stake) {
    setPrevProps({ open, deck, stake });
    if (open) {
      setActiveDeck(deck);
      setActiveStake(stake);
    }
  }

  if (!open) return null;

  const deckIdx = DECK_OPTIONS.indexOf(activeDeck) >= 0 ? DECK_OPTIONS.indexOf(activeDeck) : 0;
  const stakeIdx = STAKE_OPTIONS.indexOf(activeStake) >= 0 ? STAKE_OPTIONS.indexOf(activeStake) : 0;

  const nextDeck = () => setActiveDeck(DECK_OPTIONS[(deckIdx + 1) % DECK_OPTIONS.length]);
  const prevDeck = () => setActiveDeck(DECK_OPTIONS[(deckIdx - 1 + DECK_OPTIONS.length) % DECK_OPTIONS.length]);

  const nextStake = () => setActiveStake(STAKE_OPTIONS[(stakeIdx + 1) % STAKE_OPTIONS.length]);
  const prevStake = () => setActiveStake(STAKE_OPTIONS[(stakeIdx - 1 + STAKE_OPTIONS.length) % STAKE_OPTIONS.length]);

  const handleApply = () => {
    onChange(activeDeck, activeStake);
    onClose();
  };


  return (
    <JimboModal open={open} onClose={onClose}>
      <div style={{
        width: 600,
        maxWidth: "95vw",
        background: JimboColorOption.TEAL_GREY, // A greenish-grey slate color
        borderRadius: 16,
        border: `3px solid ${JimboColorOption.BORDER_SILVER}`,
        boxShadow: `inset 0 0 0 2px ${JimboColorOption.DARKEST}, 0 8px 16px rgba(0,0,0,0.5)`,
        padding: "24px 20px",
        display: "flex",
        flexDirection: "column",
        gap: 24,
        position: "relative",
      }}>
        {/* Tabs (Decorative to match screenshot) */}
        <div style={{
          position: "absolute",
          top: -20,
          left: 0,
          right: 0,
          display: "flex",
          justifyContent: "center",
          gap: 8,
        }}>
          <div className="j-btn j-btn--red" style={{ height: 36, pointerEvents: "none" }}>
            <div className="j-btn__face" style={{ fontSize: 16, padding: "0 16px" }}>Seed Finder</div>
          </div>
        </div>

        {/* Deck Carousel */}
        <div style={{ display: "flex", height: 160, gap: 12 }}>
          <CarouselButton onClick={prevDeck}>&lt;</CarouselButton>
          
          <div style={{
            flex: 1,
            background: JimboColorOption.DARK_GREY,
            borderRadius: 12,
            border: `2px solid ${JimboColorOption.PANEL_EDGE}`,
            display: "flex",
            alignItems: "center",
            padding: "16px",
            gap: 16,
          }}>
            <DeckSprite deck={activeDeck} size={100} style={{ flexShrink: 0 }} />
            
            <div style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center" }}>
              <h2 style={{
                fontFamily: "m6x11plus, monospace",
                fontSize: 28,
                color: JimboColorOption.WHITE,
                margin: "0 0 8px 0",
                textShadow: `1px 1px 0 ${JimboColorOption.DARKEST}`,
              }}>
                {activeDeck} Deck
              </h2>
              
              <div style={{
                background: JimboColorOption.WHITE,
                borderRadius: 8,
                padding: "12px",
                width: "100%",
                minHeight: 80,
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                boxShadow: `inset 0 -2px 0 0 ${JimboColorOption.PANEL_EDGE}`,
              }}>
                <div style={{
                  fontFamily: "m6x11plus, monospace",
                  fontSize: 16,
                  color: JimboColorOption.DARKEST,
                  textAlign: "center",
                  whiteSpace: "pre-line",
                  lineHeight: 1.2,
                }}>
                  {formatTextWithColors(DECK_DESCRIPTIONS[activeDeck] || "Standard 52 card deck")}
                </div>
              </div>
            </div>
            
            {/* Scroll indicator dots */}
            <div style={{ display: 'flex', flexDirection: 'column', gap: 4, paddingRight: 4 }}>
               {/* Decorative dots for Balatro style */}
               <div style={{ width: 8, height: 8, borderRadius: 4, background: JimboColorOption.GREY }}></div>
               <div style={{ width: 8, height: 8, borderRadius: 4, background: JimboColorOption.GREY }}></div>
               <div style={{ width: 8, height: 8, borderRadius: 4, background: JimboColorOption.GREEN }}></div>
               <div style={{ width: 8, height: 8, borderRadius: 4, background: JimboColorOption.RED }}></div>
               <div style={{ width: 8, height: 8, borderRadius: 4, background: JimboColorOption.WHITE }}></div>
            </div>
          </div>
          
          <CarouselButton onClick={nextDeck}>&gt;</CarouselButton>
        </div>

        {/* Stake Carousel */}
        <div style={{ display: "flex", height: 120, gap: 12 }}>
          <CarouselButton onClick={prevStake}>&lt;</CarouselButton>
          
          <div style={{
            flex: 1,
            background: JimboColorOption.DARK_GREY,
            borderRadius: 12,
            border: `2px solid ${JimboColorOption.PANEL_EDGE}`,
            display: "flex",
            alignItems: "center",
            padding: "12px 16px",
            gap: 16,
            position: "relative",
          }}>
             <div style={{
               position: 'absolute',
               left: -14, // Cut out effect text
               top: '50%',
               transform: 'translateY(-50%) rotate(-90deg)',
               fontFamily: "m6x11plus, monospace",
               fontSize: 16,
               color: JimboColorOption.GREY,
             }}>
               Stake
             </div>
             
             <div style={{ paddingLeft: 20 }}>
                <StakeSprite stake={activeStake} width={64} style={{ flexShrink: 0 }} />
             </div>
            
            <div style={{ flex: 1, display: "flex", flexDirection: "column", alignItems: "center" }}>
              <h2 style={{
                fontFamily: "m6x11plus, monospace",
                fontSize: 22,
                color: JimboColorOption.WHITE,
                margin: "0 0 6px 0",
                textShadow: `1px 1px 0 ${JimboColorOption.DARKEST}`,
              }}>
                {activeStake} Stake
              </h2>
              
              <div style={{
                background: JimboColorOption.WHITE,
                borderRadius: 8,
                padding: "8px 12px",
                width: "100%",
                minHeight: 60,
                display: "flex",
                flexDirection: "column",
                alignItems: "center",
                justifyContent: "center",
                boxShadow: `inset 0 -2px 0 0 ${JimboColorOption.PANEL_EDGE}`,
              }}>
                <div style={{
                  fontFamily: "m6x11plus, monospace",
                  fontSize: 14,
                  color: JimboColorOption.DARKEST,
                  textAlign: "center",
                  whiteSpace: "pre-line",
                  lineHeight: 1.2,
                }}>
                  {formatTextWithColors(STAKE_DESCRIPTIONS[activeStake] || "Base Difficulty")}
                </div>
              </div>
            </div>
          </div>
          
          <CarouselButton onClick={nextStake}>&gt;</CarouselButton>
        </div>

        {/* Buttons */}
        <div style={{ display: "flex", flexDirection: "column", alignItems: "center", gap: 12, marginTop: 8 }}>
          <button
            onClick={handleApply}
            className="j-btn j-btn--blue j-btn--lg"
            style={{ width: "80%", height: 50, fontSize: 24, padding: 0 }}
          >
            <div className="j-btn__face" style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
               APPLY
            </div>
          </button>
          
          <button
            onClick={onClose}
            className="j-btn j-btn--orange j-btn--md"
            style={{ width: "100%", height: 40, fontSize: 18, padding: 0 }}
          >
            <div className="j-btn__face" style={{ display: 'flex', alignItems: 'center', justifyContent: 'center' }}>
               Back
            </div>
          </button>
        </div>

      </div>
    </JimboModal>
  );
}
