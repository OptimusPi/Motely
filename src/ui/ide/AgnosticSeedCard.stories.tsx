import type { Meta, StoryObj } from '@storybook/react';
import { AgnosticSeedCard } from './AgnosticSeedCard';

const meta = {
  title: 'Legacy / AgnosticSeedCard',
  component: AgnosticSeedCard,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 360, padding: 16 }}><Story /></div>,
  ],
} satisfies Meta<typeof AgnosticSeedCard>;

export default meta;
type Story = StoryObj<typeof meta>;

const previewItems = [
  { name: 'Blueprint', matched: true },
  { name: 'Perkeo', matched: true },
  { name: 'Crystal Ball' },
  { name: 'The Fool' },
  { name: 'Oops! All 6s' },
];

export const DailyRitual: Story = {
  args: {
    seed: 'WEEJOKER',
    deckSlug: 'Erratic',
    stakeSlug: 'Gold',
    previewItems,
    result: { score: 124_400 } as never,
  },
};

export const Locked: Story = {
  args: {
    seed: 'TOMORROW',
    deckSlug: 'Erratic',
    stakeSlug: 'White',
    isLocked: true,
    dayNumber: 7,
  },
};
