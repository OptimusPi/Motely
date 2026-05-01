import type { Meta, StoryObj } from "@storybook/react";
import { JamlCurator } from "./JamlCurator.js";

const meta: Meta<typeof JamlCurator> = {
  title: "JAML/JamlCurator",
  component: JamlCurator,
  parameters: {
    layout: "fullscreen",
  },
};

export default meta;
type Story = StoryObj<typeof JamlCurator>;

export const Default: Story = {
  args: {},
};
