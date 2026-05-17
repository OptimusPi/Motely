import type { Meta, StoryObj } from '@storybook/react';
import { JamlMapEditor } from './JamlMapEditor';
import { JimboBackground } from '../../ui/jimboBackground';
import { useState } from 'react';

const meta = {
  title: 'JamlMap/JamlMapEditor',
  component: JamlMapEditor,
  parameters: { jimboHarness: false, 
    layout: 'fullscreen',
  },
  decorators: [
    (Story) => (
      <div style={{ width: '100vw', height: '100vh', display: 'flex', alignItems: 'center', justifyContent: 'center', position: 'relative' }}>
        <JimboBackground />
        <div style={{ width: 375, height: 667, position: 'relative', zIndex: 1, overflow: 'hidden', flexShrink: 0 }}>
          <Story />
        </div>
      </div>
    ),
  ],
} satisfies Meta<typeof JamlMapEditor>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  render: () => {
    // eslint-disable-next-line react-hooks/rules-of-hooks
    const [, setJamlStr] = useState<string>("");

    return (
      <JamlMapEditor
        onChange={setJamlStr}
      />
    );
  },
};

