import React, { useState } from "react";
import { JimboModal, JimboButton } from "../ui/panel.js";
import { JimboText } from "../ui/jimboText.js";
import { DECK_OPTIONS, STAKE_OPTIONS } from "../lib/data/constants.js";
import { DeckSprite } from "./DeckSprite.js";
import { StakeSprite } from "../ui/sprites.js";

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
  "Red": "Small Blind gives\nno reward money",
  "Green": "Required score scales\nfaster for each Ante",
  "Black": "Shop can have Jokers\nwith Eternal",
  "Blue": "-1 Discard",
  "Purple": "Required score scales\nfaster for each Ante",
  "Orange": "Shop can have Jokers\nwith Perishable",
  "Gold": "-1 hand size\nShop can have Jokers\nwith Rental",
};

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

  // Sync state if props change when opened
  React.useEffect(() => {
    if (open) {
      // eslint-disable-next-line react-hooks/set-state-in-effect
      setActiveDeck(deck);
      setActiveStake(stake);
    }
  }, [open, deck, stake]);

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
    <JimboModal open={open} onClose={onClose} title="Run Config">
      {/* Deck carousel */}
      <div className="j-flex j-items-center j-gap-sm">
        <JimboButton tone="red" size="sm" onClick={prevDeck}>&lt;</JimboButton>
        <div className="j-flex-col j-items-center j-gap-xs" style={{ flex: 1 }}>
          <DeckSprite deck={activeDeck} size={64} />
          <JimboText size="md" tone="white">{activeDeck} Deck</JimboText>
          <JimboText size="micro" tone="grey" className="j-text-center" style={{ whiteSpace: "pre-line" }}>
            {DECK_DESCRIPTIONS[activeDeck] || "Standard 52 card deck"}
          </JimboText>
        </div>
        <JimboButton tone="red" size="sm" onClick={nextDeck}>&gt;</JimboButton>
      </div>

      {/* Stake carousel */}
      <div className="j-flex j-items-center j-gap-sm" style={{ marginTop: 8 }}>
        <JimboButton tone="red" size="sm" onClick={prevStake}>&lt;</JimboButton>
        <div className="j-flex-col j-items-center j-gap-xs" style={{ flex: 1 }}>
          <StakeSprite stake={activeStake} width={48} />
          <JimboText size="md" tone="white">{activeStake} Stake</JimboText>
          <JimboText size="micro" tone="grey" className="j-text-center" style={{ whiteSpace: "pre-line" }}>
            {STAKE_DESCRIPTIONS[activeStake] || "Base Difficulty"}
          </JimboText>
        </div>
        <JimboButton tone="red" size="sm" onClick={nextStake}>&gt;</JimboButton>
      </div>

      {/* Actions */}
      <div className="j-flex-col j-gap-sm" style={{ marginTop: 8 }}>
        <JimboButton tone="blue" size="lg" fullWidth onClick={handleApply}>Apply</JimboButton>
        <JimboButton tone="orange" size="lg" fullWidth onClick={onClose}>Back</JimboButton>
      </div>
    </JimboModal>
  );
}
