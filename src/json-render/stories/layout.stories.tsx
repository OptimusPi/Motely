import type { Meta, StoryObj } from "@storybook/react-vite";
import { Panel, Stack, Grid, Text, Badge } from "../components/layout.js";

const meta: Meta<typeof Panel> = {
  title: "Wire Format/Layout",
};

export default meta;

export const PanelDefault: StoryObj<typeof Panel> = {
  render: () => (
    <Panel title="Panel Title" subtitle="Subtitle">
      <Text body="Panel content goes here." variant="body" />
    </Panel>
  ),
};

export const PanelAccent: StoryObj<typeof Panel> = {
  render: () => (
    <Panel title="Accent Panel" variant="accent">
      <Text body="This panel uses the accent variant." variant="body" />
    </Panel>
  ),
};

export const StackAndText: StoryObj<typeof Stack> = {
  render: () => (
    <Stack gap={16}>
      <Text body="Title text" variant="title" />
      <Text body="Body text" variant="body" />
      <Text body="Muted text" variant="muted" />
      <Text body="Accent text" variant="accent" />
      <Text body="Error text" variant="error" />
    </Stack>
  ),
};

export const GridAndBadges: StoryObj<typeof Grid> = {
  render: () => (
    <Grid columns={3} gap={12}>
      <Badge label="Red" tone="red" />
      <Badge label="Blue" tone="blue" />
      <Badge label="Green" tone="green" />
      <Badge label="Orange" tone="orange" />
      <Badge label="Gold" tone="gold" />
      <Badge label="Purple" tone="purple" />
    </Grid>
  ),
};
