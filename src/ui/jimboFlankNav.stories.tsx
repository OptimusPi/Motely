import type { Meta, StoryObj } from "@storybook/react-vite";
import { JimboFlankNav } from "./jimboFlankNav.js";
import { JimboText } from "./jimboText.js";

const meta: Meta<typeof JimboFlankNav> = {
  title: "Primitives/Layout/JimboFlankNav",
  component: JimboFlankNav,
};
export default meta;
type Story = StoryObj<typeof JimboFlankNav>;

export const Default: Story = {
  render: () => (
    <JimboFlankNav onPrev={() => {}} onNext={() => {}} canPrev canNext>
      <JimboText size="md" tone="white">
        Center content
      </JimboText>
    </JimboFlankNav>
  ),
};
