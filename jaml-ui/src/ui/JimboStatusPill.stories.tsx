import type { Meta, StoryObj } from '@storybook/react-vite';
import { JimboStatusPill } from './JimboStatusPill';
import { JimboRow } from './JimboLayout';

const meta = {
  title: 'Jimbo/Feedback/Status Pill',
  component: JimboStatusPill,
} satisfies Meta<typeof JimboStatusPill>;
export default meta;

export const AllStates: StoryObj<typeof meta> = {
  render: () => (
    <JimboRow gap="sm">
      <JimboStatusPill status="idle" label="Ready" />
      <JimboStatusPill status="running" label="Searching..." />
      <JimboStatusPill status="ok" label="Done" />
      <JimboStatusPill status="error" label="Error" />
      <JimboStatusPill status="paused" label="Paused" />
    </JimboRow>
  ),
};
