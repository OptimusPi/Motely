import type { Meta, StoryObj } from '@storybook/react-vite';
import { Card3D } from './Card3D';

/**
 * A 3D Balatro card that leans toward your cursor — the "magnetic tilt" the DOM
 * can't do smoothly. Move the mouse across the canvas and watch it follow.
 *
 * Renders the real sprite art via jaml-ui/core metadata; no placeholder.
 */
const meta = {
  title: 'r3f/Card3D',
  component: Card3D,
  parameters: { layout: 'centered' },
  decorators: [
    (Story) => (
      <div style={{ width: 320, height: 380 }}>
        <Story />
      </div>
    ),
  ],
  args: { itemName: 'Blueprint', height: '100%' },
} satisfies Meta<typeof Card3D>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Joker: Story = { args: { itemName: 'Blueprint' } };
export const Tarot: Story = { args: { itemName: 'The Fool', fallbackSheet: 'Tarots' } };
export const Voucher: Story = { args: { itemName: 'Overstock', fallbackSheet: 'Vouchers' } };

/** Unknown names fall back to the sheet's mystery card instead of crashing. */
export const UnknownFallsBackToMystery: Story = {
  args: { itemName: 'Definitely Not A Real Joker' },
};
