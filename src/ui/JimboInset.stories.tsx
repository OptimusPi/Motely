import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboInset } from "./JimboInset.js";
import { JimboText } from "./jimboText.js";

const meta: Meta<typeof JimboInset> = {
  title: "Primitives/Layout/JimboInset",
  component: JimboInset,
};
export default meta;
type Story = StoryObj<typeof JimboInset>;

export const Default: Story = {
  render: () => (
    <JimboInset style={{ width: 320, padding: 12 }}>
      <JimboText size="sm" tone="grey">
        Sunken well for code rows, logs, recent finds.
      </JimboText>
    </JimboInset>
  ),
};
