import type { Meta, StoryObj } from '@storybook/react';
import { JimboBalatroFooter } from './footer';

const meta = {
  title: 'JimboUI / JimboBalatroFooter',
  component: JimboBalatroFooter,
  parameters: {
    jimboHarness: false,
    layout: 'fullscreen',
  },
  decorators: [
    (Story) => <div style={{ position: 'relative', width: 320, height: 120, margin: '0 auto', background: '#1e2b2d' }}><Story /></div>,
  ],
} satisfies Meta<typeof JimboBalatroFooter>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {};
