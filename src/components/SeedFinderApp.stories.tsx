import type { Meta, StoryObj } from '@storybook/react-vite';
import { SeedFinderApp } from './SeedFinderApp';

const meta = {
  title: 'APPS / Seed Finder',
  component: SeedFinderApp,
  // SeedFinderApp renders its own JimboApp shell, so tell the preview
  // harness not to double-wrap it (which would render two footers).
  parameters: { jimboHarness: false },
} satisfies Meta<typeof SeedFinderApp>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};

export const BlackDeckGoldStake: Story = {
  args: { initialDeck: 'Black', initialStake: 'Gold' },
};

export const CustomFilter: Story = {
  args: {
    initialJaml: `must:
  - joker: Perkeo
    antes: [1, 2, 3, 4, 5, 6, 7, 8]
should:
  - voucher: Hieroglyph
    antes: [1]
`,
    initialDeck: 'Red',
    initialStake: 'White',
  },
};
