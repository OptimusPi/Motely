import type { Meta, StoryObj } from '@storybook/react';
import { JamlMapEditor } from './JamlMapEditor';
import { useState } from 'react';

import "../../ui/jimbo.css"; // Ensure global CSS is loaded

const meta = {
  title: 'JamlMap/JamlMapEditor',
  component: JamlMapEditor,
  parameters: {
    layout: 'fullscreen',
    viewport: {
      defaultViewport: 'mobile1', // iPhone SE
    },
  },
  decorators: [
    (Story) => (
      <div style={{ width: "100%", height: "100dvh", background: "#111" }}>
        <Story />
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
