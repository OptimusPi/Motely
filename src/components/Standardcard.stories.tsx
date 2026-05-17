import type { Meta, StoryObj } from '@storybook/react';
import { RealStandardcard } from './Standardcard';

function Showcase() {
  return (
    <div style={{ display: 'flex', gap: 12, flexWrap: 'wrap', justifyContent: 'center', width: 320 }}>
      <RealStandardcard rank="Ace" suit="Spades" />
      <RealStandardcard rank="10" suit="Hearts" enhancement="gold" />
      <RealStandardcard rank="Q" suit="Diamonds" seal="red" />
      <RealStandardcard rank="J" suit="Clubs" edition="Foil" />
    </div>
  );
}

const meta = {
  title: 'JAML / Standardcard',
  component: RealStandardcard,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof RealStandardcard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ShowcaseCards: Story = {
  render: () => <Showcase />,
};
