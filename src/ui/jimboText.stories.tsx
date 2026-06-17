import type { Meta, StoryObj } from '@storybook/react-vite';
import { JimboText } from './jimboText';
import { JimboApp } from './jimboApp';
import { JimboStack } from './jimboLayout';

/**
 * The only sanctioned text primitive. Every player-facing string in jaml-ui goes
 * through it. Never write `<span style={{ fontFamily: 'm6x11plus' }}>` — use
 * `<JimboText>`. Never use ALL CAPS or bold (design rule, lint-enforced).
 *
 * Size scale (small → large): micro, label, xs, body, sm, md, heading, lg, xl, display.
 * Tone is semantic (gold = price/title, green = success, red = mult, etc.) — not theme-toggleable.
 */
const meta = {
  title: 'JimboUI / JimboText',
  component: JimboText,
} satisfies Meta<typeof JimboText>;

export default meta;
type Story = StoryObj<typeof meta>;

// Realistic in-context use — what future-Claude needs to see, not a tone catalog.
export const InContext: Story = {
  render: () => (
    <JimboApp>
      <JimboStack gap="md">
        <JimboText as="h1" size="display" tone="gold">$487</JimboText>
        <JimboText as="h2" size="heading" tone="white">Wee Joker</JimboText>
        <JimboText size="md" tone="grey">Common joker, +8 Mult each played 2.</JimboText>
        <JimboText size="sm" tone="green">Found: ABCD1234</JimboText>
        <JimboText size="xs" tone="red">−2 hands remaining</JimboText>
      </JimboStack>
    </JimboApp>
  ),
};

// Size reference — single render so it's one visual lookup, not 10 stories.
export const SizeReference: Story = {
  render: () => (
    <JimboApp>
      <JimboStack gap="sm">
        <JimboText size="display">display 26px</JimboText>
        <JimboText size="xl">xl 24px</JimboText>
        <JimboText size="lg">lg 18px</JimboText>
        <JimboText size="heading">heading 14px</JimboText>
        <JimboText size="md">md 14px</JimboText>
        <JimboText size="sm">sm 12px</JimboText>
        <JimboText size="body">body 12px</JimboText>
        <JimboText size="xs">xs 11px</JimboText>
        <JimboText size="label">label 10px</JimboText>
        <JimboText size="micro">micro 9px</JimboText>
      </JimboStack>
    </JimboApp>
  ),
};

// The two flags that change behavior (not just appearance).
export const Dancing: Story = {
  args: { children: 'Dancing letters', size: 'lg', tone: 'gold', dance: true },
};
