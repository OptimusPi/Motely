import type { Meta, StoryObj } from '@storybook/react';
import { JimboBackground } from './jimboBackground';

/** Canonical Balatro-ish defaults — mirrors hook BALATRO_DEFAULTS for control baselines. */
const DEFAULT_ARGS = {
  primary: '#ff3333',
  secondary: '#0080ff',
  dark: '#0d1419',
  speed: 1,
  spinRotation: -2,
  spinAmount: 0.35,
  pixelFilter: 244.2,
  contrast: 4.5,
  lighting: 0.5,
  transitionMs: 800,
} as const;

const meta = {
  title: 'JimboUI / JimboBackground',
  component: JimboBackground,
  parameters: {
    jimboHarness: false,
    jimboBackground: false,
    layout: 'fullscreen',
  },
  args: { ...DEFAULT_ARGS },
  render: (args) => <JimboBackground {...args} />,
  argTypes: {
    primary: { control: 'color', description: 'Swirl primary (lerps)' },
    secondary: { control: 'color', description: 'Swirl secondary (lerps)' },
    dark: { control: 'color', description: 'Shadow / tertiary (lerps)' },
    speed: {
      control: { type: 'range', min: 0.25, max: 3, step: 0.05 },
      description: 'Animation speed multiplier (lerps)',
    },
    spinRotation: {
      control: { type: 'range', min: -4, max: 4, step: 0.1 },
      description: 'Twirl rotation seed (lerps)',
    },
    spinAmount: {
      control: { type: 'range', min: 0, max: 1, step: 0.05 },
      description: 'Twirl warp amount (lerps)',
    },
    pixelFilter: {
      control: { type: 'range', min: 80, max: 900, step: 5 },
      description: 'Higher = finer pixels; drag to see pixel block transition',
    },
    contrast: {
      control: { type: 'range', min: 1, max: 8, step: 0.1 },
      description: 'Contrast multiplier (lerps)',
    },
    lighting: {
      control: { type: 'range', min: 0, max: 1.5, step: 0.05 },
      description: 'Highlight strength (lerps)',
    },
    transitionMs: {
      control: { type: 'range', min: 0, max: 3000, step: 50 },
      description: 'Ease duration for all prop changes (0 = snap)',
    },
  },
} satisfies Meta<typeof JimboBackground>;

export default meta;
type Story = StoryObj<typeof meta>;

/** Full shader playground — use Controls to scrub pixelFilter, palette, spin, etc. */
export const Playground: Story = {};

export const Palette: Story = {
  args: {
    primary: '#ff3344',
    secondary: '#0088ff',
    dark: '#0a1018',
  },
};

export const CoarsePixels: Story = {
  args: { pixelFilter: 150 },
};

export const FinePixels: Story = {
  args: { pixelFilter: 740 },
};

export const HighSpin: Story = {
  args: { speed: 1.5, spinAmount: 0.5 },
};

export const Snappy: Story = {
  args: { transitionMs: 0 },
};

export const SlowEase: Story = {
  args: { transitionMs: 2000, pixelFilter: 400 },
};
