import type { Meta, StoryObj } from '@storybook/react-vite';
import { JimboDivider } from './JimboDivider';
import { JimboSpacer } from './JimboSpacer';
import { JimboText } from './jimboText';
import { JimboRow, JimboStack } from './JimboLayout';

const meta = {
  title: 'Jimbo/Layout/Divider & Spacer',
  component: JimboDivider,
} satisfies Meta<typeof JimboDivider>;
export default meta;

export const Horizontal: StoryObj<typeof meta> = {
  render: () => (
    <JimboStack gap="sm" align="stretch">
      <JimboText>Above the rule</JimboText>
      <JimboDivider />
      <JimboText>Below the rule</JimboText>
    </JimboStack>
  ),
};

export const Vertical: StoryObj<typeof meta> = {
  render: () => (
    <JimboRow gap="md" align="stretch">
      <JimboText>Left</JimboText>
      <JimboDivider vert />
      <JimboText>Right</JimboText>
    </JimboRow>
  ),
};

export const SpacerSizes: StoryObj<typeof meta> = {
  render: () => (
    <JimboStack gap="xs" align="stretch">
      <JimboText>Tight</JimboText>
      <JimboSpacer size={8} />
      <JimboText>Loose</JimboText>
      <JimboSpacer size={32} />
      <JimboText>Done</JimboText>
    </JimboStack>
  ),
};
