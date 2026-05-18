import type { Meta, StoryObj } from '@storybook/react';
import { RealStandardcard } from './Standardcard';
import { JimboPanel } from '../ui/panel';
import { JimboText } from '../ui/jimboText';

function Showcase() {
  return (
    <JimboPanel className="j-story-panel-grid">
      <JimboText size="md" tone="gold" className="j-section-header__tag">Standard Cards (standardCard)</JimboText>
      <div className="j-story-card-grid">
        <RealStandardcard rank="Ace" suit="Spades" size={48} />
        <RealStandardcard rank="10" suit="Hearts" enhancement="gold" size={48} />
        <RealStandardcard rank="Q" suit="Diamonds" seal="red" size={48} />
        <RealStandardcard rank="J" suit="Clubs" edition="Foil" size={48} />
        <RealStandardcard rank="King" suit="Hearts" enhancement="steel" edition="Holographic" size={48} />
      </div>
    </JimboPanel>
  );
}

const meta = {
  title: 'JAML / Standard Cards (standardCard)',
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
