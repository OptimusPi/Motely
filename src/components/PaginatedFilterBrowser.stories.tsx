import type { Meta, StoryObj } from '@storybook/react';
import { PaginatedFilterBrowser } from './PaginatedFilterBrowser';

const filterSeeds = [
  ["Erratic deck", "Gold stake", "Wee Monday", ["Wee Joker", "Blueprint", "Ankh", "Ouija"]],
  ["Ghost deck", "Orange stake", "Perkeo Observatory", ["Perkeo", "Observatory", "Planet X"]],
  ["Checkered deck", "Black stake", "Baron Mime", ["Baron", "Mime", "Steel King"]],
  ["Plasma deck", "White stake", "Negative Perkeo", ["Negative", "Perkeo", "Soul"]],
  ["Abandoned deck", "Purple stake", "Sixth Sense Chain", ["Sixth Sense", "Spectral Pack"]],
  ["Anaglyph deck", "Blue stake", "Voucher Ladder", ["Overstock", "Clearance", "Liquidation"]],
] as const;

const filters = Array.from({ length: 18 }, (_, index) => {
  const template = filterSeeds[index % filterSeeds.length];
  return {
    id: `filter-${index + 1}`,
    deckText: template[0],
    stakeText: template[1],
    name: template[2],
    targetItems: [...template[3]],
    description: `Looks for ${template[3].join(", ")} with a playable ${template[0].toLowerCase()} route.`,
    authorText: `Curated route ${index + 1}`,
    dateText: `2026-05-${String((index % 9) + 10)}`,
    statsText: `${(index + 1) * 3} hits`,
  };
});

const meta = {
  title: 'JAML / PaginatedFilterBrowser',
  component: PaginatedFilterBrowser,
} satisfies Meta<typeof PaginatedFilterBrowser>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    filters,
    itemsPerPage: 6,
    showSecondary: true,
    showDelete: true,
  },
};
