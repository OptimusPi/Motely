import type { Meta, StoryObj } from '@storybook/react';
import { CategoryPicker, VOUCHER_PICKER_CONFIG } from './CategoryPicker';

const meta = {
  title: 'JAML / CategoryPicker',
  component: CategoryPicker,
} satisfies Meta<typeof CategoryPicker>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Vouchers: Story = {
  args: {
    config: VOUCHER_PICKER_CONFIG,
    onSelect: () => undefined,
  },
};
