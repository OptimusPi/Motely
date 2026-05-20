import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JimboPanel } from '../ui/panel';
import { JamlCodeEditor } from './JamlCodeEditor';

const SAMPLE = `must:\n  - joker: Blueprint\nshould:\n  - uncommonJoker: Any\n`;

function Demo() {
  const [value, setValue] = useState(SAMPLE);

  return (
    <div style={{ flex: 1, minHeight: 0, display: 'flex', flexDirection: 'column' }}>
      <JimboPanel style={{ flex: 1, minHeight: 0 }}>
        <JamlCodeEditor value={value} onChange={setValue} />
      </JimboPanel>
    </div>
  );
}

const meta = {
  title: 'JAML / JamlCodeEditor',
  component: JamlCodeEditor,
} satisfies Meta<typeof JamlCodeEditor>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Demo />,
};
