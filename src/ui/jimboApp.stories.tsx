<<<<<<< HEAD
import type { Meta, StoryObj } from '@storybook/react';
=======
import type { Meta, StoryObj } from '@storybook/react-vite';
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
import { JimboApp, JimboAppScroll, JimboAppFooter } from './jimboApp';
import { JimboPanel, JimboButton } from './panel';
import { JimboText } from './jimboText';
import { JimboStack } from './jimboLayout';

const meta = {
  title: 'JimboUI / JimboApp',
  component: JimboApp,
} satisfies Meta<typeof JimboApp>;

export default meta;
type Story = StoryObj<typeof meta>;

/**
 * The canonical 320×568 locked shell. This is the surface every primitive is
 * designed against — the MCP Apps inline-iframe target. No scroll, no
 * stretch, no reflow.
 */
export const Default: Story = {
  render: () => (
    <JimboApp>
      <JimboPanel>
        <JimboText size="lg" tone="white">JimboApp shell</JimboText>
        <JimboText size="sm" tone="grey">320×568 locked. The canonical surface.</JimboText>
      </JimboPanel>
    </JimboApp>
  ),
};

/**
<<<<<<< HEAD
 * Fluid variant — unlocks for MCP / desktop contexts where the host doesn't
 * impose a fixed widget size. Container queries in jimbo.css activate "cozy"
 * overrides at 401px+.
 */
export const Fluid: Story = {
  args: { fluid: true },
  render: (args) => (
    <JimboApp {...args}>
      <JimboPanel>
        <JimboText size="lg" tone="white">Fluid shell</JimboText>
        <JimboText size="sm" tone="grey">Stretches up to ~750px max.</JimboText>
      </JimboPanel>
    </JimboApp>
  ),
};

/**
=======
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
 * With JimboAppScroll inside — the scrollable content area, hidden scrollbar,
 * magnetic snap.
 */
export const Scrollable: StoryObj = {
  render: () => (
    <JimboApp>
      <JimboAppScroll>
        <JimboStack gap="md">
          {Array.from({ length: 20 }, (_, i) => (
            <JimboPanel key={i}>
              <JimboText size="md" tone="white">Card {i + 1}</JimboText>
              <JimboText size="xs" tone="grey">Scrollable content inside the locked shell.</JimboText>
            </JimboPanel>
          ))}
        </JimboStack>
      </JimboAppScroll>
    </JimboApp>
  ),
};

/**
 * With sticky JimboAppFooter — the thumb-zone action bar that doesn't scroll
 * with the body content.
 */
export const WithStickyFooter: StoryObj = {
  render: () => (
    <JimboApp>
      <JimboAppScroll>
        <JimboStack gap="md">
          {Array.from({ length: 8 }, (_, i) => (
            <JimboPanel key={i}>
              <JimboText size="md" tone="white">Section {i + 1}</JimboText>
            </JimboPanel>
          ))}
        </JimboStack>
      </JimboAppScroll>
      <JimboAppFooter>
        <JimboButton tone="green" fullWidth>Confirm</JimboButton>
      </JimboAppFooter>
    </JimboApp>
  ),
};
