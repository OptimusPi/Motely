import type { Meta, StoryObj } from '@storybook/react-vite';
import { useState } from 'react';
import { JamlCodeEditor } from './JamlCodeEditor';

const meta = {
  title: 'Screens/JAML IDE/Code Editor',
  component: JamlCodeEditor,
} satisfies Meta<typeof JamlCodeEditor>;
export default meta;

// Seeded so you can immediately exercise the jaml-lang typeahead:
//  • put the cursor after `joker: ` and type `Bl` → Blueprint / BlueJoker narrow in
//  • change `Blueprint` to a bogus name → the linter underlines it (red squiggle)
//  • hit Ctrl-Space on an empty value for the full context-aware list
const SAMPLE = `name: Typeahead Demo
deck: Red
stake: White

must:
  - joker: Blueprint
    antes: [1, 2]

should:
  - voucher: Telescope
    score: 100
`;

export const Playground: StoryObj<typeof meta> = {
  render: () => {
    const [value, setValue] = useState(SAMPLE);
    return <JamlCodeEditor value={value} onChange={setValue} minHeight={420} />;
  },
};
