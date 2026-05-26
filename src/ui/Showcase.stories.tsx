import type { Meta, StoryObj } from '@storybook/react';
import { Showcase } from './showcase';

const meta = {
  title: 'JAML / Showcase',
  component: Showcase,
  parameters: {
    layout: 'fullscreen',
    // Showcase owns its own <JimboApp> shell + footer, so skip the decorator
    // harness to avoid a double-wrap that re-paints the dark teal under us.
    jimboHarness: false,
  },
} satisfies Meta<typeof Showcase>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    title: 'Balatro',
    subtitle: 'Seed Curator',
    mcpInfo: {
      engine: 'Motely WASM 0.1.0',
      runtime: 'Ready',
      features: 'Full Search',
    },
    hotFilters: [
      {
        name: 'Gros Michel Hunter',
        author: 'Localthunk',
        hits: '1.2K',
        tone: 'red',
        sample: ['grosmichel', 'joker'],
      },
      {
        name: 'Blueprint Start',
        author: 'Jimbo',
        hits: '950',
        tone: 'blue',
        sample: ['blueprint', 'joker'],
      },
      {
        name: 'Oops! All Sixes',
        author: 'Community',
        hits: '420',
        tone: 'gold',
        sample: ['six', 'six'],
      },
      {
        name: 'Vampire Run',
        author: 'BloodSucker',
        hits: '120',
        tone: 'purple',
        sample: ['vampire', 'joker'],
      },
    ],
    recentFinds: [
      { seed: 'A1B2C3D4', filterName: 'Gros Michel Hunter', score: 15 },
      { seed: '88888888', filterName: 'Oops! All Sixes', score: 10 },
      { seed: 'VAMP1R3S', filterName: 'Vampire Run', score: 0 },
    ],
  },
};
