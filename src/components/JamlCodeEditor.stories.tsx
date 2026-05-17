import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JimboPanel } from '../ui/panel';
import { JamlCodeEditor } from './JamlCodeEditor';

const SAMPLE = `must:\n  - joker: Blueprint\nshould:\n  - uncommonJoker: Any\n`;

function Demo() {
  const [value, setValue] = useState(SAMPLE);

  return (
    <div style={{ width: 300, height: 420 }}>
      <JimboPanel style={{ height: '100%' }}>
        <JamlCodeEditor value={value} onChange={setValue} />
      </JimboPanel>
    </div>
  );
}

const meta = {
  title: 'JAML / JamlCodeEditor',
  component: JamlCodeEditor,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof JamlCodeEditor>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Demo />,
};
