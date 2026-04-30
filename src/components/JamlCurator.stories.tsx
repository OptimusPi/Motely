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
  args: {
    motelyWasmUrl: "https://unpkg.com/@nims11/motely@0.2.2/motely_bg.wasm",
  },
};
