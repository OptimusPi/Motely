import type { Meta, StoryObj } from '@storybook/react';
import React, { useState } from 'react';
import { JamlSeedInput } from './JamlSeedInput';
import { JamlSeedSpinner } from './JamlSeedSpinner';

function ControlledSeedInput(props: React.ComponentProps<typeof JamlSeedInput>) {
  const [seed, setSeed] = useState(props.value ?? '');

  return <JamlSeedInput {...props} value={seed} onChange={setSeed} />;
}

function ControlledSeedSpinner(props: React.ComponentProps<typeof JamlSeedSpinner>) {
  const [seed, setSeed] = useState(props.value ?? 'ALEEB');

  return <JamlSeedSpinner {...props} value={seed} onChange={setSeed} />;
}

const meta = {
  title: 'JAML / JamlSeedInput',
  component: JamlSeedInput,
  parameters: { jimboHarness: false, 
    layout: 'centered',
  },
} satisfies Meta<typeof JamlSeedInput>;

export default meta;
type Story = StoryObj<typeof meta>;

export const Normal: Story = {
  render: () => <ControlledSeedInput variant="normal" placeholder="Aleeb" />,
};

export const Dark: Story = {
  render: () => <ControlledSeedInput variant="dark" value="FROGMANS" placeholder="Frogmans" />,
};

export const Alt: Story = {
  render: () => <ControlledSeedInput variant="alt" value="PILUVYOU" placeholder="Piluvyou" />,
};

export const Spinner: Story = {
  render: () => (
    <ControlledSeedSpinner
      variant="normal"
      seeds={['ALEEB', 'FROGMANS', 'PILUVYOU']}
      value="ALEEB"
      placeholder="Aleeb"
    />
  ),
};

