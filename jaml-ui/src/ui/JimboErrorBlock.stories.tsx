import type { Meta, StoryObj } from '@storybook/react-vite';
import { JimboErrorBlock } from './JimboErrorBlock';

const meta = {
  title: 'Jimbo/Feedback/Error Block',
  component: JimboErrorBlock,
} satisfies Meta<typeof JimboErrorBlock>;
export default meta;

export const Default: StoryObj<typeof meta> = {
  render: () => (
    <JimboErrorBlock title="Search failed">
      Motely timed out after 30s. Lower the max ante and try again.
    </JimboErrorBlock>
  ),
};

export const Dismissible: StoryObj<typeof meta> = {
  render: () => (
    <JimboErrorBlock title="Invalid JAML" onDismiss={() => {}}>
      Unexpected token on line 4 — did you mean `should:`?
    </JimboErrorBlock>
  ),
};
