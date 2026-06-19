import type { Meta, StoryObj } from '@storybook/react-vite';
import { JimboInputModal } from './JimboInputModal';

const meta = {
  title: 'JimboUI / JimboInputModal',
  component: JimboInputModal,
  parameters: {
    jimboHarness: true,
    layout: 'fullscreen',
  },
} satisfies Meta<typeof JimboInputModal>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    open: true,
    title: 'Rename filter',
    message: 'Give this route a readable name.',
    placeholder: 'Blueprint opener',
    onCancel: () => undefined,
    onConfirm: () => undefined,
  },
};
