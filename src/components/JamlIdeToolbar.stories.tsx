import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JimboPanel } from '../ui/panel';
import { JamlIdeToolbar, type JamlIdeMode } from './JamlIdeToolbar';

function Demo() {
  const [mode, setMode] = useState<JamlIdeMode>('code');

  return (
    <div style={{ width: '100%' }}>
      <JimboPanel>
        <JamlIdeToolbar
          mode={mode}
          onModeChange={setMode}
          resultCount={12}
          showResultsTab
          showJamlyzerTab
          onSearch={() => undefined}
          onLoadFile={() => undefined}
        />
      </JimboPanel>
    </div>
  );
}

const meta = {
  title: 'JAML / JamlIdeToolbar',
  component: JamlIdeToolbar,
} satisfies Meta<typeof JamlIdeToolbar>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => <Demo />,
};
