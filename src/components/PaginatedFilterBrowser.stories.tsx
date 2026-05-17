import type { Meta, StoryObj } from '@storybook/react';
import { PaginatedFilterBrowser } from './PaginatedFilterBrowser';

const filters = Array.from({ length: 18 }, (_, index) => ({
  id: `filter-${index + 1}`,
  name: `Filter ${index + 1}`,
  description: `Search route ${index + 1} with boosted odds and cleaner shops.`,
  authorText: `Author ${index + 1}`,
  dateText: `2026-05-${String((index % 9) + 10)}`,
  statsText: `${(index + 1) * 3} hits`,
}));

const meta = {
  title: 'JAML / PaginatedFilterBrowser',
  component: PaginatedFilterBrowser,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
  decorators: [
    (Story) => <div style={{ width: 320, height: 568 }}><Story /></div>,
  ],
} satisfies Meta<typeof PaginatedFilterBrowser>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: {
    filters,
    itemsPerPage: 6,
    showSecondary: true,
    showDelete: true,
  },
};
