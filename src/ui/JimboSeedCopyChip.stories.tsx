import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboSeedCopyChip } from "./JimboSeedCopyChip.js";

const meta: Meta<typeof JimboSeedCopyChip> = {
  title: "Primitives/Display/JimboSeedCopyChip",
  component: JimboSeedCopyChip,
};
export default meta;
type Story = StoryObj<typeof JimboSeedCopyChip>;

export const Default: Story = {
  render: () => <JimboSeedCopyChip value="ALEEB123" />,
};

export const Empty: Story = {
  render: () => <JimboSeedCopyChip value="" placeholder="--------" />,
};
