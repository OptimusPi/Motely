import type { Meta, StoryObj } from '@storybook/react';
import { useState } from 'react';
import { PanelSplitter } from './PanelSplitter';

function Demo() {
  const [left, setLeft] = useState(140);
  return (
    <div style={{ display: 'flex', width: 320, height: 180, background: '#1e2b2d' }}>
      <div style={{ width: left, background: '#3a5055' }} />
      <PanelSplitter orientation="vertical" onDrag={(delta) => setLeft((value) => Math.max(80, Math.min(240, value + delta)))} />
      <div style={{ flex: 1, background: '#404c4e' }} />
    </div>
  );
}

const meta = {
  title: 'JimboUI / PanelSplitter',
  component: PanelSplitter,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof PanelSplitter>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Vertical: Story = {
  render: () => <Demo />,
};
