import type { Meta, StoryObj } from '@storybook/react-vite';
import { JimboGrid } from './JimboGrid';
import { JimboBadge } from './JimboBadge';

const meta = {
  title: 'Jimbo/Layout/Grid',
  component: JimboGrid,
} satisfies Meta<typeof JimboGrid>;
export default meta;

const cells = ['One', 'Two', 'Three', 'Four', 'Five', 'Six', 'Seven'];

export const ThreeColumns: StoryObj<typeof meta> = {
  render: () => (
    <JimboGrid columns={3} gap="md">
      {cells.map((c) => (
        <JimboBadge key={c} tone="blue" size="md">
          {c}
        </JimboBadge>
      ))}
    </JimboGrid>
  ),
};

export const AutoFit: StoryObj<typeof meta> = {
  render: () => (
    <JimboGrid minColWidth={120} gap="lg">
      {cells.map((c) => (
        <JimboBadge key={c} tone="green" size="md">
          {c}
        </JimboBadge>
      ))}
    </JimboGrid>
  ),
};
