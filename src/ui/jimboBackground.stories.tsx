import type { Meta, StoryObj } from '@storybook/react';
import { JimboBackground } from './jimboBackground';
import { JimboText } from './jimboText';

const meta = {
  title: 'JimboUI / JimboBackground',
  component: JimboBackground,
  parameters: {
    jimboHarness: false,
    layout: 'fullscreen',
  },
} satisfies Meta<typeof JimboBackground>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => (
    <div style={{ minHeight: '100vh', position: 'relative' }}>
      <JimboBackground />
      <div style={{ position: 'relative', zIndex: 1, display: 'flex', minHeight: '100vh', alignItems: 'center', justifyContent: 'center' }}>
        <JimboText size="display" tone="gold">Background</JimboText>
      </div>
    </div>
  ),
};
