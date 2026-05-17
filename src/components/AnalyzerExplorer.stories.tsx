import type { Meta, StoryObj } from '@storybook/react';
import { AnalyzerExplorer, type AnalyzerAnteView, type AnalyzerHighlight } from './AnalyzerExplorer';

const antes: AnalyzerAnteView[] = [
  {
    ante: 0,
    voucher: 'Crystal Ball',
    smallBlindTag: 'Uncommon Tag',
    bigBlindTag: 'Rare Tag',
    boss: 'The Hook',
    packs: ['Arcana Pack', 'Buffoon Pack'],
    shop: [
      { id: 'a0-j1', name: 'Blueprint', value: 0, desired: true, detail: 'Copies right Joker' },
      { id: 'a0-v1', name: 'Blank', badges: [{ label: 'voucher' }] },
    ],
    facts: [{ label: 'Goal', value: 'Open with Blueprint' }],
  },
  {
    ante: 1,
    voucher: 'Overstock',
    smallBlindTag: 'Investment Tag',
    bigBlindTag: 'Voucher Tag',
    boss: 'The Wall',
    packs: ['Celestial Pack'],
    shop: [
      { id: 'a1-j1', name: 'Oops! All 6s', desired: true, badges: [{ label: 'uncommon' }] },
      { id: 'a1-j2', name: 'Perkeo', badges: [{ label: 'legendary', tone: 'accent' }] },
    ],
    facts: [{ label: 'Money', value: '$18' }],
  },
  {
    ante: 2,
    voucher: 'Antimatter',
    smallBlindTag: 'Negative Tag',
    bigBlindTag: 'Skip Tag',
    boss: 'The Needle',
    packs: ['Spectral Pack', 'Mega Buffoon Pack'],
    shop: [
      { id: 'a2-j1', name: 'Joker', badges: [{ label: 'common' }] },
      { id: 'a2-j2', name: 'The Soul', detail: 'Pack hit' },
    ],
    facts: [{ label: 'Plan', value: 'Stabilize economy' }],
  },
];

const highlights: AnalyzerHighlight[] = [
  { id: 'h1', ante: 0, title: 'Blueprint opener', subtitle: 'Voucher + Blueprint line', desired: true, item: { id: 'h1i', name: 'Blueprint', desired: true } },
  { id: 'h2', ante: 1, title: 'Oops! All 6s', subtitle: 'Uncommon spike', desired: true, item: { id: 'h2i', name: 'Oops! All 6s', desired: true } },
  { id: 'h3', ante: 2, title: 'Needle check', subtitle: 'Boss routing', boss: 'The Needle' },
];

const meta = {
  title: 'JAML / AnalyzerExplorer',
  component: AnalyzerExplorer,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320, height: 568 }}><Story /></div>,
  ],
} satisfies Meta<typeof AnalyzerExplorer>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    antes,
    highlights,
    jaml: 'must:\n  - joker: Blueprint\nshould:\n  - uncommonJoker: Oops! All 6s\n',
  },
};
