import type { Meta, StoryObj } from '@storybook/react';
import { JamlAestheticSelector } from './JamlAestheticSelector';
import React, { useState } from 'react';
import type { JamlAestheticOption } from './JamlAestheticSelector';

const meta = {
  title: 'JAML / JamlAestheticSelector',
  component: JamlAestheticSelector,
  argTypes: {
    onChange: { action: 'onChange' },
  },
} satisfies Meta<typeof JamlAestheticSelector>;

export default meta;
type Story = StoryObj<typeof meta>;

type SelectorArgs = React.ComponentProps<typeof JamlAestheticSelector> & {
  onChange: (value: JamlAestheticOption | null, numericValue: number) => void;
};

function StatefulSelector(args: SelectorArgs) {
  const [value, setValue] = useState<JamlAestheticOption | null>(args.value ?? null);
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
}

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

