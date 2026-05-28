// TODO(jimbo-primitives): pre-dates no-inline-style / no-token-in-jsx-style /
// no-inline-component rules. Refactor to compose from Jimbo* primitives once
// screenshot-driven primitive design lands. `git grep TODO(jimbo-primitives)`.
/* eslint-disable jaml-design/no-inline-component */

import { useState } from "react";
import { JimboModal, JimboButton } from "../ui/panel.js";
import { JimboPanelSpinner } from "../ui/JimboPanelSpinner.js";
import { MotelyDeck, MotelyStake } from "motely-wasm";
const DECK_OPTIONS = Object.keys(MotelyDeck).filter(k => isNaN(Number(k)));
const STAKE_OPTIONS = Object.keys(MotelyStake).filter(k => isNaN(Number(k)));
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
  return (
    <JimboModal open={open} onClose={onClose} title="Run Config">
      {open ? (
        <RunConfigModalBody
          deck={deck}
          stake={stake}
          onChange={onChange}
          onClose={onClose}
        />
      ) : null}
    </JimboModal>
  );
}

interface RunConfigModalBodyProps {
  deck: string;
  stake: string;
  onChange: (deck: string, stake: string) => void;
  onClose: () => void;
}

function RunConfigModalBody({ deck, stake, onChange, onClose }: RunConfigModalBodyProps) {
  const [activeDeck, setActiveDeck] = useState(deck);
  const [activeStake, setActiveStake] = useState(stake);

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
    <>
      <JimboPanelSpinner
        label="Deck"
        title={`${activeDeck} Deck`}
        description={DECK_DESCRIPTIONS[activeDeck] || "Standard 52 card deck"}
        media={<DeckSprite deck={activeDeck} size={64} />}
        onPrev={prevDeck}
        onNext={nextDeck}
      />

      <JimboPanelSpinner
        label="Stake"
        title={`${activeStake} Stake`}
        description={STAKE_DESCRIPTIONS[activeStake] || "Base Difficulty"}
        media={<StakeSprite stake={activeStake} width={48} />}
        onPrev={prevStake}
        onNext={nextStake}
        className="j-mt-sm"
      />

      {/* Actions — JimboModal already renders a Back button via showBack,
          so we only emit Apply here. Two stacked "Back" buttons was a bug. */}
      <div className="j-flex-col j-gap-sm j-mt-sm">
        <JimboButton tone="blue" size="lg" fullWidth onClick={handleApply}>Apply</JimboButton>
      </div>
    </>
  );
}
