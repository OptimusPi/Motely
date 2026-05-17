import type { Meta, StoryObj } from '@storybook/react';
import { JamlMapEditor } from './JamlMapEditor';
import { useState } from 'react';

const meta = {
  title: 'JamlMap/JamlMapEditor',
  component: JamlMapEditor,
  parameters: { jimboHarness: true,
    layout: 'fullscreen',
  },
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

