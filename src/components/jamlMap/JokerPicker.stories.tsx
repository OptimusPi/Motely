import type { Meta, StoryObj } from '@storybook/react';
import { JokerPicker } from './JokerPicker';

const meta = {
  title: 'JAML / JokerPicker',
  component: JokerPicker,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320, height: 520 }}><Story /></div>,
  ],
} satisfies Meta<typeof JokerPicker>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    onSelect: () => undefined,
  },
};
