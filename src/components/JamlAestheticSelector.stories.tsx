import type { Meta, StoryObj } from '@storybook/react';
import { JamlAestheticSelector } from './JamlAestheticSelector';
import React, { useState } from 'react';

const meta = {
  title: 'JAML / JamlAestheticSelector',
  component: JamlAestheticSelector,
  parameters: {
    layout: 'centered',
  },
  argTypes: {
    onChange: { action: 'onChange' },
  },
} satisfies Meta<typeof JamlAestheticSelector>;

export default meta;
type Story = StoryObj<typeof meta>;

const StatefulSelector = (args: any) => {
  const [value, setValue] = useState<any>(args.value || null);
  return (
    <JamlAestheticSelector
      {...args}
      value={value}
      onChange={(val, numVal) => {
        setValue(val);
        args.onChange(val, numVal);
      }}
    />
  );
};

export const Default: Story = {
  render: (args) => <StatefulSelector {...args} />,
  args: {
    value: null,
  },
};

export const WithSelection: Story = {
  render: (args) => <StatefulSelector {...args} />,
  args: {
    value: 'Palindrome',
  },
};
