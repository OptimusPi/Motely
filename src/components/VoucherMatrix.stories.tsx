import type { Meta, StoryObj } from '@storybook/react';
import { JimboSprite } from '../ui/sprites';
import { JimboPanel } from '../ui/panel';
import { JimboText } from '../ui/jimboText';
import { JimboBadge } from '../ui/JimboBadge';

const anteRewards = [
  { ante: 1, base: '$300' },
  { ante: 2, base: '$1,000' },
  { ante: 3, base: '$3,200' },
  { ante: 4, base: '$9,000' },
  { ante: 5, base: '$25,000' },
  { ante: 6, base: '$60,000' },
  { ante: 7, base: '$110,000' },
  { ante: 8, base: '$200,000' },
];

const vouchers = [
  'Overstock', 'Clearance Sale', 'Tarot Merchant', 'Planet Merchant',
  'Hone', 'Reroll Surplus', 'Crystal Ball', 'Telescope',
  'Grabber', 'Wasteful', 'Seed Money', 'Blank',
  'Magic Trick', 'Hieroglyph', 'Director\'s Cut', 'Paint Brush',
];

function VoucherMatrix() {
  return (
    <JimboPanel style={{ padding: 8 }}>
      <div style={{ display: 'flex', gap: 6 }}>
        {/* Left rail — ante ladder */}
        <div style={{
          display: 'flex',
          flexDirection: 'column',
          gap: 4,
          padding: '4px 6px',
          background: 'var(--j-darkest)',
          borderRadius: 'var(--j-radius-md)',
          border: '2px solid var(--j-panel-edge)',
        }}>
          <div style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
            <JimboText size="micro" tone="white">Ante</JimboText>
            <JimboText size="micro" tone="white">Base</JimboText>
          </div>
          {anteRewards.map(({ ante, base }) => (
            <div key={ante} style={{ display: 'flex', justifyContent: 'space-between', gap: 8 }}>
              <JimboText size="sm" tone="white">{ante}</JimboText>
              <JimboText size="sm" tone="red">{base}</JimboText>
            </div>
          ))}
        </div>

        {/* Right side — blind chips header + voucher grid */}
        <div style={{ display: 'flex', flexDirection: 'column', gap: 6, flex: 1, minWidth: 0 }}>
          <div style={{ display: 'flex', justifyContent: 'flex-end', gap: 4 }}>
            <JimboBadge tone="blue" size="md">Small Blind</JimboBadge>
            <JimboBadge tone="red" size="md">Big Blind</JimboBadge>
          </div>
          <div style={{
            display: 'grid',
            gridTemplateColumns: 'repeat(4, 1fr)',
            gap: 3,
          }}>
            {vouchers.map((v) => (
              <div
                key={v}
                style={{
                  display: 'flex',
                  alignItems: 'center',
                  justifyContent: 'center',
                  padding: 1,
                }}
                aria-label={v}
              >
                <JimboSprite name={v} sheet="Vouchers" width={30} />
              </div>
            ))}
          </div>
        </div>
      </div>
    </JimboPanel>
  );
}

const meta = {
  title: 'JAML / VoucherMatrix',
  parameters: {
    jimboHarness: true,
    layout: 'fullscreen',
  },
} satisfies Meta;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <VoucherMatrix />,
};
