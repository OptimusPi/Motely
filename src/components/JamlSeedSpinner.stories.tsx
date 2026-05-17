import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JamlSeedSpinner } from './JamlSeedSpinner';

function SpinnerDemo(props: React.ComponentProps<typeof JamlSeedSpinner>) {
  const [seed, setSeed] = useState(props.value ?? 'ALEEB');

  return <JamlSeedSpinner {...props} value={seed} onChange={setSeed} />;
}

const meta = {
  title: 'JAML / JamlSeedSpinner',
  component: JamlSeedSpinner,
  parameters: {
    jimboHarness: false,
    layout: 'centered',
  },
} satisfies Meta<typeof JamlSeedSpinner>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Normal: Story = {
  render: () => <SpinnerDemo seeds={['ALEEB', 'FROGMANS', 'PILUVYOU']} variant="normal" />,
};

export const Dark: Story = {
  render: () => <SpinnerDemo seeds={['ALEEB', 'FROGMANS', 'PILUVYOU']} variant="dark" value="FROGMANS" />,
};

export const Alt: Story = {
  render: () => <SpinnerDemo seeds={['ALEEB', 'FROGMANS', 'PILUVYOU']} variant="alt" value="PILUVYOU" />,
};
