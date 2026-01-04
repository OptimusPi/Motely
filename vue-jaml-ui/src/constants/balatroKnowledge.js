/**
 * Balatro Game Knowledge Base
 * Comprehensive data about Jokers, Vouchers, and Core Mechanics
 * Used by JAML Genie for accurate, detailed responses
 */

export const jokers = {
  blueprint: {
    name: "Blueprint",
    id: "blueprint",
    effect: "Copies the ability of the Joker to its immediate right (excluding passive modifier effects).",
    trigger_condition: "Activates whenever a hand is played; effect is determined by the Joker to the right at the moment of scoring.",
    scaling_behavior: "Copies only the trigger-based effect, not passive effects (e.g., does not copy hand size changes, end-of-round triggers, or debuffed Jokers). If copying a scaling Joker, only the final result is copied, not the scaling process.",
    synergies: [
      "Brainstorm (stacking copy effects)",
      "Baron (multiplicative scaling)",
      "Mime (doubling retrigger effects)",
      "Jokers with X Mult or retrigger effects"
    ],
    anti_synergies: [
      "Jokers with passive effects (e.g., Chicot, Pareidolia)",
      "End-of-round Jokers (e.g., Golden Joker)",
      "Debuffed Jokers"
    ],
    rarity: "Rare",
    cost: 10,
    sell_price: 5,
    unlock_condition: "Win 1 run",
    compatibility: {
      editions: ["Foil", "Holo", "Polychrome", "Negative"],
      seals: "Not applicable",
      stickers: ["Perishable", "Eternal"]
    },
    special_interactions: [
      "When combined with Brainstorm, each copy adds one more instance of the copied effect.",
      "Copying chance-based Jokers (e.g., Space Joker) rolls chance independently for each copy.",
      "Copying Stuntman only copies the +250 Chips, not the -2 hand size penalty.",
      "Selling Blueprint when copying Luchador or Diet Cola triggers their sell effect.",
      "Moving Blueprint during a round changes the copied effect for subsequent hands.",
      "Edge cases: Boss blinds that shuffle Jokers can cause Blueprint to copy nothing if moved to rightmost slot."
    ],
    version_notes: "As of v1.0.1f, compatibility and copy logic are unchanged. Tooltip shows incompatible Jokers.",
    source_refs: ["https://balatrogame.fandom.com/wiki/Blueprint"]
  },
  baron: {
    name: "Baron",
    id: "baron",
    effect: "Each King held in hand gives X1.5 Mult (multiplicative).",
    trigger_condition: "Triggers after playing a hand, for each King held in hand (not discarded or debuffed).",
    scaling_behavior: "Multiplies score exponentially for each King; e.g., 2 Kings = x2.25, 3 Kings = x3.375, etc.",
    synergies: [
      "Painted Deck (+2 hand size)",
      "Mime (doubles multiplier per King)",
      "Reserved Parking (money for face cards held)",
      "Midas Mask (buffs scored Kings)",
      "Shoot the Moon (multiplies Queen mult by Baron)",
      "Juggler, Troubadour (increase hand size)",
      "Plasma Deck (exponential scaling)"
    ],
    anti_synergies: [
      "Abandoned Deck (no Kings)",
      "The Plant blind (debuffed Kings)"
    ],
    rarity: "Rare",
    cost: 8,
    sell_price: 4,
    unlock_condition: "Available from start",
    compatibility: {
      editions: "All compatible",
      seals: "Red Seal retriggers effect per King",
      stickers: ["Perishable", "Eternal"]
    },
    special_interactions: [
      "Mime increases exponent of multiplier.",
      "Blueprint/Brainstorm can copy Baron for additional scaling.",
      "Red Seal retriggers held-in-hand effects, stacking with Mime."
    ],
    version_notes: "No major changes in recent patches.",
    source_refs: ["https://balatrogame.fandom.com/wiki/Baron"]
  },
  stuntman: {
    name: "Stuntman",
    id: "stuntman",
    effect: "+250 Chips per hand played; -2 hand size penalty.",
    trigger_condition: "Activates independently each hand played.",
    scaling_behavior: "Flat chip bonus; hand size penalty is passive and not copied by Blueprint.",
    synergies: [
      "Plasma Deck (balances chips for high scores)",
      "Painted Deck (+2 hand size neutralizes penalty)",
      "Juggler, Turtle Bean, Troubadour (increase hand size)",
      "Blueprint/Brainstorm (copies chip bonus, not penalty)",
      "Blackboard (smaller hand size triggers condition)",
      "Raised Fist (efficient scoring with small hands)"
    ],
    anti_synergies: [
      "Merry Andy (-1 hand size, may make hands unplayable)",
      "Ouija, Ectoplasm (further reduce hand size)",
      "The Manacle, The Psychic blinds (hand size requirements)"
    ],
    rarity: "Rare",
    cost: 7,
    sell_price: 3,
    unlock_condition: "Earn at least 100 million Chips in a single hand",
    compatibility: {
      editions: "All compatible",
      seals: "Not applicable",
      stickers: ["Perishable", "Eternal"]
    },
    special_interactions: [
      "Blueprint copies only the chip bonus, not the hand size penalty.",
      "Selling Stuntman via Judgement Tarot triggers chip bonus for current hand, hand size drops next draw."
    ],
    version_notes: "v1.0.1f reduced chip bonus from +300 to +250, increased rarity and price.",
    source_refs: ["https://balatrogame.fandom.com/wiki/Stuntman"]
  },
  supernova: {
    name: "Supernova",
    id: "supernova",
    effect: "Adds the number of times poker hand has been played this run to Mult.",
    trigger_condition: "Retroactive - counts all previous plays of the hand type on pickup.",
    scaling_behavior: "Additive Mult that scales with consistency. Rewards playing the same hand type repeatedly.",
    synergies: [
      "Checkered Deck (Hearts+Spades only) => easy flush strategy",
      "Burglar (converts discards → extra hands, more plays per round)",
      "Green Joker (also scales with hands played)",
      "Card Sharp (X3 Mult when repeating same hand in round)",
      "Space Joker (upgrades repeated hand type levels)",
      "Burnt Joker (levels a hand without planets)"
    ],
    anti_synergies: [
      "Obelisk (wants diverse hand types, conflicting with Supernova's consistency requirement)",
      "Throwback (skipping blinds to level Throwback reduces total hands played)"
    ],
    rarity: "Common",
    cost: 5,
    sell_price: 2,
    unlock_condition: "Available from start",
    compatibility: {
      editions: "All compatible",
      seals: "Not applicable",
      stickers: ["Perishable", "Eternal"]
    },
    special_interactions: [
      "Retroactive scaling makes it valuable even if acquired late in a run.",
      "Especially potent in builds focused on flushes or pairs.",
      "Effect is immediate upon purchase."
    ],
    version_notes: "No major changes in recent patches.",
    source_refs: ["https://balatrogame.fandom.com/wiki/Supernova"]
  },
  cavendish: {
    name: "Cavendish",
    id: "cavendish",
    effect: "X3 Mult. 1 in 1000 chance this card is destroyed at the end of round.",
    trigger_condition: "Independent activation each hand.",
    scaling_behavior: "Fixed X3 multiplier. With copies (Blueprint/Brainstorm), each gives another X3 (X9, X27, etc.).",
    synergies: [
      "Any deck (unconditional x3 helps all decks)",
      "Blueprint/Brainstorm (exponential stacking)",
      "Fortune Teller",
      "Supernova",
      "Other additive_mult_scalers"
    ],
    anti_synergies: [
      "Oops! All 6s (doubles probabilities, making destruction chance 1/500)"
    ],
    rarity: "Common",
    cost: 4,
    sell_price: 2,
    unlock_condition: "Available from start, but shop spawn gated behind Gros Michel self-destruction this run",
    compatibility: {
      editions: "All compatible",
      seals: "Not applicable",
      stickers: ["Perishable"],
      eternal: false
    }
  },
  fortune_teller: {
    name: "Fortune Teller",
    id: "fortune_teller",
    effect: "+1 Mult per Tarot card used this run. (Retroactive)",
    trigger_condition: "Retroactive - counts all previously used Tarots at pickup.",
    scaling_behavior: "Additive Mult scaling with tarot usage count.",
    synergies: [
      "Zodiac Deck (starts with Tarot Merchant voucher)",
      "Vagabond",
      "Cartomancer",
      "Hallucination",
      "8 Ball",
      "Superposition",
      "Vampire (consumes enhancements, pairs with enhancement-focused Tarots)"
    ],
    rarity: "Common",
    cost: 6,
    sell_price: 3,
    unlock_condition: "Available from start",
    compatibility: {
      editions: "All compatible",
      seals: "Not applicable",
      stickers: ["Perishable", "Eternal"]
    },
    special_interactions: [
      "Using Tarot cards on dummy cards still increases Mult."
    ],
    version_notes: "No major changes in recent patches.",
    source_refs: ["https://balatrogame.fandom.com/wiki/Fortune_Teller"]
  },
  ramen: {
    name: "Ramen",
    id: "ramen",
    effect: "X2 Mult, loses X0.01 Mult per card discarded.",
    trigger_condition: "Independent, but scales down with discards.",
    scaling_behavior: "Reverse scaling - strictly decreases with discards. Starts at X2.0, minimum X1.0 (destroys itself at X1.0 after 100 discards).",
    synergies: [
      "Green Deck (rewards conserving discards)",
      "Blue Deck (+1 hand/round makes it easier to use 'throwaway' hands)",
      "Nebula Deck (Telescope voucher helps find planets quickly)",
      "Painted Deck, Checkered Deck, Abandoned Deck (boost consistency)",
      "Banner (+chips per unused discard pairs naturally with Ramen's anti-discard playstyle)"
    ],
    anti_synergies: [
      "Red Deck (encourages discards for value, quickly decaying Ramen)",
      "Black Deck (fewer hands per round forces more discards)"
    ],
    rarity: "Uncommon",
    cost: 6,
    sell_price: 3,
    unlock_condition: "Available from start",
    compatibility: {
      editions: "All compatible",
      seals: "Not applicable",
      stickers: ["Perishable"],
      eternal: false
    },
    special_interactions: [
      "Sell value never decreases regardless of current Mult.",
      "\"Eaten!\" message when destroyed after 100 discards."
    ],
    version_notes: "No major changes in recent patches.",
    source_refs: ["https://balatrogame.fandom.com/wiki/Ramen"]
  },
  sock_and_buskin: {
    name: "Sock and Buskin",
    id: "sock_and_buskin",
    effect: "Retrigger all played face cards.",
    trigger_condition: "On played face cards (J, Q, K) that are scoring in current hand.",
    scaling_behavior: "Power scales with number and quality of face cards + other multipliers/retriggers.",
    synergies: [
      "Hanging Chad (retriggers first scoring card again)",
      "Photograph (first face card gives X2 Mult; retriggering amplifies)",
      "Bloodstone (Heart faces with X2 Mult chance; extra retriggers roll more chances)",
      "Scary Face, Smiley Face",
      "Shoot the Moon",
      "Baron",
      "Glass Card (retriggering glass face cards can skyrocket Mult)",
      "Red Seal (retrigger face cards multiple times via seal + Joker retriggers)"
    ],
    rarity: "Uncommon",
    cost: 6,
    sell_price: 3,
    unlock_condition: "Play a total of 300 face cards",
    compatibility: {
      editions: "All compatible",
      seals: "Red Seal especially powerful",
      stickers: ["Perishable", "Eternal"]
    }
  }
}

export const vouchers = {
  overstock: {
    name: "Overstock",
    id: "overstock",
    effect: "Add +1 card slot to the shop (3 total).",
    upgraded: {
      name: "Overstock Plus",
      effect: "Add +1 more slot (4 total).",
      unlock_condition: "Spend a total of $2,500 at the shop."
    }
  },
  clearance_sale: {
    name: "Clearance Sale",
    id: "clearance_sale",
    effect: "All cards and packs in the shop are 25% off.",
    upgraded: {
      name: "Liquidation",
      effect: "All cards and packs in the shop are 50% off.",
      unlock_condition: "Redeem at least 10 vouchers in a single run."
    }
  },
  hone: {
    name: "Hone",
    id: "hone",
    effect: "Foil, Holographic, and Polychrome cards appear 2× as often.",
    upgraded: {
      name: "Glow Up",
      effect: "Foil, Holographic, and Polychrome cards appear 4× as often.",
      unlock_condition: "Have ≥5 Jokers with Foil/Holo/Polychrome (or Negative) effects."
    }
  },
  reroll_surplus: {
    name: "Reroll Surplus",
    id: "reroll_surplus",
    effect: "Rerolls cost $2 less.",
    upgraded: {
      name: "Reroll Glut",
      effect: "Rerolls cost an additional $2 less.",
      unlock_condition: "Reroll the shop 100 times total."
    }
  },
  crystal_ball: {
    name: "Crystal Ball",
    id: "crystal_ball",
    effect: "+1 consumable slot.",
    upgraded: {
      name: "Omen Globe",
      effect: "Spectral cards may appear in Arcana Booster Packs.",
      unlock_condition: "Use 25 Tarot cards from booster packs."
    }
  },
  telescope: {
    name: "Telescope",
    id: "telescope",
    effect: "Celestial packs always contain the Planet card for your most-played hand this run.",
    upgraded: {
      name: "Observatory",
      effect: "Planet cards in your consumables area give X1.5 Mult for their specified hand.",
      unlock_condition: "Use 25 Planet cards from booster packs."
    }
  },
  grabber: {
    name: "Grabber",
    id: "grabber",
    effect: "Permanently gain +1 hand per round.",
    upgraded: {
      name: "Nacho Tong",
      effect: "Permanently gain another +1 hand per round.",
      unlock_condition: "Play 2,500 cards total."
    }
  },
  wasteful: {
    name: "Wasteful",
    id: "wasteful",
    effect: "Permanently gain +1 discard per round.",
    upgraded: {
      name: "Recyclomancy",
      effect: "Permanently gain another +1 discard per round.",
      unlock_condition: "Discard 2,500 cards total."
    }
  },
  tarot_merchant: {
    name: "Tarot Merchant",
    id: "tarot_merchant",
    effect: "Tarot cards appear 2× as often in the shop.",
    upgraded: {
      name: "Tarot Tycoon",
      effect: "Tarot cards appear 4× as often.",
      unlock_condition: "Buy 50 Tarot cards from the shop."
    }
  },
  planet_merchant: {
    name: "Planet Merchant",
    id: "planet_merchant",
    effect: "Planet cards appear 2× as often in the shop.",
    upgraded: {
      name: "Planet Tycoon",
      effect: "Planet cards appear 4× as often.",
      unlock_condition: "Buy 50 Planet cards from the shop."
    }
  },
  seed_money: {
    name: "Seed Money",
    id: "seed_money",
    effect: "Raise interest cap per round to $10.",
    upgraded: {
      name: "Money Tree",
      effect: "Raise interest cap per round to $20.",
      unlock_condition: "Hit max interest for 10 consecutive rounds."
    }
  },
  blank: {
    name: "Blank",
    id: "blank",
    effect: "Does nothing.",
    upgraded: {
      name: "Antimatter",
      effect: "+1 Joker slot.",
      unlock_condition: "Redeem Blank 10 times across runs."
    }
  },
  magic_trick: {
    name: "Magic Trick",
    id: "magic_trick",
    effect: "Playing cards can be purchased individually from the shop.",
    upgraded: {
      name: "Illusion",
      effect: "Cards bought from the shop can have an Enhancement, Edition, and/or Seal.",
      unlock_condition: "Buy 20 playing cards from the shop."
    }
  },
  hieroglyph: {
    name: "Hieroglyph",
    id: "hieroglyph",
    effect: "Go back one Ante; -1 hand per round.",
    upgraded: {
      name: "Petroglyph",
      effect: "Go back one more Ante; -1 discard per round.",
      unlock_condition: "Reach Ante 12."
    }
  },
  directors_cut: {
    name: "Director's Cut",
    id: "directors_cut",
    effect: "Reroll the Boss Blind once per Ante.",
    upgraded: {
      name: "Retcon",
      effect: "Reroll Boss Blinds unlimited times (costing money per reroll).",
      unlock_condition: "Discover 25 Blinds."
    }
  }
}

export const coreMechanics = {
  scoring: {
    pipeline: [
      "Determine base Chips and base Mult from the poker hand + planet levels.",
      "Apply played-card effects (chips/mult added or multiplied).",
      "Apply in-hand effects (Steel cards, Baron, Shoot the Moon, etc.).",
      "Apply Joker effects in order from left to right.",
      "Final score = total_chips × total_mult."
    ],
    formula: {
      chips_final: "sum(played_card_chips + modifiers)",
      mult_final: "base_hand_mult + additive_mult_effects; then apply multiplicative (×) effects.",
      score: "chips_final * mult_final"
    }
  },
  poker_hands_rank_order: [
    "High Card",
    "Pair",
    "Two Pair",
    "Three of a Kind",
    "Straight",
    "Flush",
    "Full House",
    "Four of a Kind",
    "Straight Flush",
    "Five of a Kind"
  ],
  shop_system: {
    shop_refresh: [
      "At the start of each round.",
      "After beating a Boss Blind."
    ],
    contents: [
      "Jokers",
      "Booster Packs (Arcana/Tarot, Celestial/Planet, Spectral, Buffoon, etc.)",
      "Playing cards (with Magic Trick/Illusion).",
      "Vouchers (one slot per Ante by default)."
    ],
    economy: {
      money_gain_sources: [
        "Blinds (Small/Big/Boss payouts).",
        "Jokers (e.g. Golden Joker, Egg, Faceless Joker, etc.).",
        "Seals (Gold Seal), Gold Cards, etc."
      ],
      interest: {
        default_cap_per_round: 3,
        interest_rate_per_$5: 1,
        modifiers: [
          "Seed Money / Money Tree raise cap."
        ]
      }
    }
  },
  discards: {
    per_round: {
      base_discards: 3
    },
    behavior: [
      "Discarded cards are replaced by a fresh draw up to hand size until deck exhausted.",
      "Some Jokers trigger on discards (Banner, Mystic Summit, Delayed Gratification, etc.).",
      "Boss Blind The Hook discards 2 random cards before each hand, which counts as discards for Ramen and Purple Seals."
    ]
  },
  hand_and_deck_limits: {
    base_hand_size: 5,
    base_joker_slots: 5,
    base_consumable_slots: 2,
    modifiers: {
      sources: [
        "Decks (Painted +2 hand size, Black +1 Joker slot, etc.).",
        "Vouchers (Grabber, Wasteful, Crystal Ball, Antimatter, Paint Brush/Palette).",
        "Negative edition on cards/Jokers/consumables."
      ]
    }
  }
}

/**
 * Search functions for knowledge base
 */
export function findJoker(nameOrId) {
  const search = nameOrId.toLowerCase().replace(/\s+/g, '_')
  return Object.values(jokers).find(j => 
    j.id === search || 
    j.name.toLowerCase() === nameOrId.toLowerCase() ||
    j.id.includes(search) ||
    j.name.toLowerCase().includes(search)
  )
}

export function findVoucher(nameOrId) {
  const search = nameOrId.toLowerCase().replace(/\s+/g, '_')
  return Object.values(vouchers).find(v => 
    v.id === search || 
    v.name.toLowerCase() === nameOrId.toLowerCase() ||
    v.id.includes(search) ||
    v.name.toLowerCase().includes(search)
  )
}

export function searchJokers(query) {
  const lowerQuery = query.toLowerCase()
  return Object.values(jokers).filter(j => 
    j.name.toLowerCase().includes(lowerQuery) ||
    j.id.includes(lowerQuery) ||
    j.effect.toLowerCase().includes(lowerQuery) ||
    j.synergies?.some(s => s.toLowerCase().includes(lowerQuery)) ||
    j.anti_synergies?.some(a => a.toLowerCase().includes(lowerQuery))
  )
}

export function getJokerSynergies(jokerName) {
  const joker = findJoker(jokerName)
  if (!joker) return null
  return {
    synergies: joker.synergies || [],
    anti_synergies: joker.anti_synergies || [],
    special_interactions: joker.special_interactions || []
  }
}

export function formatJokerInfo(joker) {
  if (!joker) return null
  
  let info = `**${joker.name}** (${joker.rarity})\n\n`
  info += `**Effect:** ${joker.effect}\n\n`
  
  if (joker.trigger_condition) {
    info += `**Trigger:** ${joker.trigger_condition}\n\n`
  }
  
  if (joker.scaling_behavior) {
    info += `**Scaling:** ${joker.scaling_behavior}\n\n`
  }
  
  if (joker.synergies && joker.synergies.length > 0) {
    info += `**Synergies:**\n${joker.synergies.map(s => `- ${s}`).join('\n')}\n\n`
  }
  
  if (joker.anti_synergies && joker.anti_synergies.length > 0) {
    info += `**Anti-Synergies:**\n${joker.anti_synergies.map(s => `- ${s}`).join('\n')}\n\n`
  }
  
  if (joker.special_interactions && joker.special_interactions.length > 0) {
    info += `**Special Interactions:**\n${joker.special_interactions.map(s => `- ${s}`).join('\n')}\n\n`
  }
  
  info += `**Cost:** $${joker.cost} | **Sell:** $${joker.sell_price}`
  
  return info
}

export function formatVoucherInfo(voucher) {
  if (!voucher) return null
  
  let info = `**${voucher.name}**\n\n`
  info += `**Effect:** ${voucher.effect}\n\n`
  
  if (voucher.upgraded) {
    info += `**Upgraded (${voucher.upgraded.name}):** ${voucher.upgraded.effect}\n`
    if (voucher.upgraded.unlock_condition) {
      info += `*Unlock: ${voucher.upgraded.unlock_condition}*\n`
    }
  }
  
  return info
}
