import type { Meta, StoryObj } from '@storybook/react';
import { DeckSprite } from '../components/DeckSprite';
import { JimboPanelSpinner } from './JimboPanelSpinner';

const meta = {
  title: 'JimboUI / JimboPanelSpinner',
  component: JimboPanelSpinner,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof JimboPanelSpinner>;

export default meta;
type Story = StoryObj<typeof meta>;

export const DeckSelector: Story = {
  args: {
    label: 'Deck',
    title: 'Erratic Deck',
    description: 'All ranks and suits in deck are randomized',
    media: <DeckSprite deck="Erratic" size={64} />,
  },
};
