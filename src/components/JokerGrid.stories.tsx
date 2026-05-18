import type { Meta, StoryObj } from '@storybook/react';
import { JimboSprite } from '../ui/sprites';
import { JimboTooltip } from '../ui/jimboTooltip';
import { JimboPanel } from '../ui/panel';

interface JokerEntry {
  name: string;
  rarity: 'common' | 'uncommon' | 'rare' | 'legendary';
  blurb: string;
}

const jokers: JokerEntry[] = [
  { name: 'Joker', rarity: 'common', blurb: '+4 Mult' },
  { name: 'Greedy Joker', rarity: 'common', blurb: 'Played Diamonds give +3 Mult' },
  { name: 'Lusty Joker', rarity: 'common', blurb: 'Played Hearts give +3 Mult' },
  { name: 'Wrathful Joker', rarity: 'common', blurb: 'Played Spades give +3 Mult' },
  { name: 'Gluttonous Joker', rarity: 'common', blurb: 'Played Clubs give +3 Mult' },
  { name: 'Jolly Joker', rarity: 'common', blurb: '+8 Mult if hand contains a Pair' },
  { name: 'Zany Joker', rarity: 'common', blurb: '+12 Mult if hand contains Three of a Kind' },
  { name: 'Mad Joker', rarity: 'common', blurb: '+10 Mult if hand contains Two Pair' },
  { name: 'Crazy Joker', rarity: 'common', blurb: '+12 Mult if hand contains a Straight' },
  { name: 'Droll Joker', rarity: 'common', blurb: '+10 Mult if hand contains a Flush' },
  { name: 'Sly Joker', rarity: 'common', blurb: '+50 Chips if hand contains a Pair' },
  { name: 'Wily Joker', rarity: 'common', blurb: '+100 Chips if hand contains Three of a Kind' },
  { name: 'Clever Joker', rarity: 'common', blurb: '+80 Chips if hand contains Two Pair' },
  { name: 'Devious Joker', rarity: 'common', blurb: '+100 Chips if hand contains a Straight' },
  { name: 'Crafty Joker', rarity: 'common', blurb: '+80 Chips if hand contains a Flush' },
  { name: 'Half Joker', rarity: 'common', blurb: '+20 Mult if played hand has 3 or fewer cards' },
  { name: 'Credit Card', rarity: 'common', blurb: 'Go up to -$20 in debt' },
  { name: 'Banner', rarity: 'common', blurb: '+30 Chips per remaining Discard' },
  { name: 'Mystic Summit', rarity: 'common', blurb: '+15 Mult when 0 Discards remaining' },
  { name: 'Blueprint', rarity: 'rare', blurb: 'Copies the ability of the Joker to the right' },
  { name: 'Brainstorm', rarity: 'rare', blurb: 'Copies the ability of the leftmost Joker' },
  { name: 'Perkeo', rarity: 'legendary', blurb: 'Creates a Negative copy of one consumable card at the end of every Shop' },
  { name: 'Triboulet', rarity: 'legendary', blurb: 'Played Kings and Queens each give X2 Mult when scored' },
  { name: 'Yorick', rarity: 'legendary', blurb: 'Gains X1 Mult every 23 cards discarded' },
  { name: 'Chicot', rarity: 'legendary', blurb: 'Disables effect of every Boss Blind' },
];

const rarityTone = {
  common: 'blue',
  uncommon: 'green',
  rare: 'red',
  legendary: 'purple',
} as const;

const rarityLabel = {
  common: 'Common',
  uncommon: 'Uncommon',
  rare: 'Rare',
  legendary: 'Legendary',
} as const;

function JokerGrid() {
  return (
    <JimboPanel style={{ padding: 8 }}>
      <div style={{
        display: 'grid',
        gridTemplateColumns: 'repeat(5, 1fr)',
        gap: 4,
      }}>
        {jokers.map((j) => (
          <JimboTooltip
            key={j.name}
            variant="card"
            badge={{ tone: rarityTone[j.rarity], label: rarityLabel[j.rarity] }}
            content={<span>{j.blurb}</span>}
          >
            <button
              type="button"
              style={{
                background: 'transparent',
                border: 'none',
                padding: 0,
                cursor: 'pointer',
              }}
              aria-label={j.name}
            >
              <JimboSprite name={j.name} sheet="Jokers" width={40} />
            </button>
          </JimboTooltip>
        ))}
      </div>
    </JimboPanel>
  );
}

const meta = {
  title: 'JAML / JokerGrid',
  parameters: {
    jimboHarness: true,
    layout: 'fullscreen',
  },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <JokerGrid />,
};
