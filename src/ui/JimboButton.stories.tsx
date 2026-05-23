import type { Meta, StoryObj } from '@storybook/react';
import { JimboButton, type JimboTone } from './JimboButton';
import { JimboApp } from './jimboApp';
import { JimboStack, JimboRow } from './jimboLayout';

const ALL_TONES: JimboTone[] = ['orange', 'red', 'blue', 'green', 'grey', 'tarot', 'planet', 'spectral'];

/**
 * The Jimbo button is R3F. Magnetic tilt on hover, spring press on click, and
 * sub-pixel idle sway at rest. There is no DOM fallback — every button is a
 * Canvas. R3F works inside MCP App iframes, mobile, and desktop.
 */
const meta = {
  title: 'JimboUI / JimboButton',
  component: JimboButton,
} satisfies Meta<typeof JimboButton>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Default: Story = {
  args: { children: 'Search' },
};

export const AllTones: Story = {
  render: () => (
    <JimboApp>
      <JimboStack gap="md" align="center">
        {ALL_TONES.map((tone) => (
          <JimboButton key={tone} tone={tone}>{tone}</JimboButton>
        ))}
      </JimboStack>
    </JimboApp>
  ),
};

export const AllSizes: Story = {
  render: () => (
    <JimboApp>
      <JimboStack gap="md" align="center">
        <JimboButton size="xs">size xs</JimboButton>
        <JimboButton size="sm">size sm</JimboButton>
        <JimboButton size="md">size md</JimboButton>
        <JimboButton size="lg">size lg</JimboButton>
      </JimboStack>
    </JimboApp>
  ),
};

export const Disabled: Story = {
  args: { children: 'Cannot click', disabled: true },
};

export const RowOfPrimaryActions: Story = {
  render: () => (
    <JimboApp>
      <JimboRow gap="md" align="center" justify="center">
        <JimboButton tone="red" size="sm">Cancel</JimboButton>
        <JimboButton tone="green" size="sm">Confirm</JimboButton>
      </JimboRow>
    </JimboApp>
  ),
};
