import type { Meta, StoryObj } from '@storybook/react-vite';
import { CardTable } from './CardTable';

/**
 * A row of grabbable 3D cards in one Canvas — the shop, not a single swatch.
 * Hover a card to make it lean toward the cursor; press and drag to lift it off
 * the felt and move it; release to drop it back in its slot.
 */
const meta = {
  title: 'r3f/CardTable',
  component: CardTable,
  parameters: { layout: 'centered' },
  decorators: [
    (Story) => (
      <div style={{ width: 600, height: 360 }}>
        <Story />
      </div>
    ),
  ],
  args: { height: '100%' },
} satisfies Meta<typeof CardTable>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ShopRow: Story = {
  args: {
    items: [
      { itemName: 'Blueprint', edition: 'foil' },
      { itemName: 'Brainstorm', edition: 'holo' },
      { itemName: 'The Fool', fallbackSheet: 'Tarots' },
      { itemName: 'Overstock', fallbackSheet: 'Vouchers', edition: 'polychrome' },
    ],
  },
};

export const TwoJokers: Story = {
  args: {
    items: [{ itemName: 'Blueprint' }, { itemName: 'Brainstorm' }],
  },
};

/** A row of legendaries — grab one and drag it; its soul floats along, parallaxing. */
export const Legendaries: Story = {
  args: {
    items: [
      { itemName: 'Canio', edition: 'holo' },
      { itemName: 'Triboulet', edition: 'foil' },
      { itemName: 'Yorick' },
      { itemName: 'Perkeo', edition: 'polychrome' },
    ],
  },
};
