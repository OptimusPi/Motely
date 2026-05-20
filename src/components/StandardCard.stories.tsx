import type { Meta, StoryObj } from '@storybook/react';
import { StandardCard } from './StandardCard';
import { CardSuit, CardRank, CardEnhancement, CardSeal, CardEdition } from './cardEnums';
import { JimboPanel } from '../ui/panel';
import { JimboText } from '../ui/jimboText';

function Showcase() {
  return (
    <JimboPanel className="j-story-panel-grid">
      <JimboText size="md" tone="gold" className="j-section-header__tag">Standard Cards</JimboText>
      <div className="j-story-card-grid">
        <StandardCard rank={CardRank.Ace} suit={CardSuit.Spades} size={48} />
        <StandardCard rank={CardRank.Ten} suit={CardSuit.Hearts} enhancement={CardEnhancement.Gold} size={48} />
        <StandardCard rank={CardRank.Queen} suit={CardSuit.Diamonds} seal={CardSeal.Red} size={48} />
        <StandardCard rank={CardRank.Jack} suit={CardSuit.Clubs} edition={CardEdition.Foil} size={48} />
        <StandardCard rank={CardRank.King} suit={CardSuit.Hearts} enhancement={CardEnhancement.Steel} edition={CardEdition.Holographic} size={48} />
      </div>
    </JimboPanel>
  );
}

const meta = {
  title: 'JAML / StandardCard',
  component: StandardCard,
} satisfies Meta<typeof StandardCard>;

export default meta;
type Story = StoryObj<typeof meta>;

export const ShowcaseCards: Story = {
  render: () => <Showcase />,
};
