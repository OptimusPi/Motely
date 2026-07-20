import type { Meta, StoryObj } from "@storybook/react-vite";
import {
  JimboPicker,
  JimboPickerSection,
  JimboPickerSearch,
  JimboPickerHint,
  JimboPickerGrid,
  JimboPickerItem,
  JimboPickerPair,
  JimboPickerEmpty,
} from "./JimboPicker.js";
import { JimboTextInput } from "./JimboTextInput.js";
import { JimboText } from "./jimboText.js";

const meta: Meta = {
  title: "Primitives/Inputs/JimboPicker",
};
export default meta;

export const Default: StoryObj = {
  render: () => (
    <JimboPicker style={{ width: 320 }}>
      <JimboPickerSection>
        <JimboPickerSearch>
          <JimboTextInput placeholder="Search..." style={{ width: "100%" }} />
        </JimboPickerSearch>
        <JimboPickerHint>Type to filter, or pick "Any"</JimboPickerHint>
        <JimboPickerGrid>
          <JimboPickerItem>Blueprint</JimboPickerItem>
          <JimboPickerItem>Brainstorm</JimboPickerItem>
          <JimboPickerItem muted>Perkeo</JimboPickerItem>
        </JimboPickerGrid>
      </JimboPickerSection>
    </JimboPicker>
  ),
};

export const Pair: StoryObj = {
  render: () => (
    <JimboPickerPair>
      <JimboText size="sm" tone="white">
        Left
      </JimboText>
      <JimboText size="sm" tone="white">
        Right
      </JimboText>
    </JimboPickerPair>
  ),
};

export const Empty: StoryObj = {
  render: () => (
    <JimboPickerEmpty>
      <JimboText size="sm" tone="grey">
        No matches
      </JimboText>
    </JimboPickerEmpty>
  ),
};
