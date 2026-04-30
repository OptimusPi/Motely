import type { Meta, StoryObj } from '@storybook/react';
import { JimboButton } from './panel';
import { JimboText } from './jimboText';
import "./jimbo.css";

const meta = {
  title: 'JimboUI/Core',
  parameters: {
    layout: 'centered',
  },
} satisfies Meta;

export default meta;

export const Typography: StoryObj = {
  render: () => (
    <div className="j-panel" style={{ display: "flex", flexDirection: "column", gap: 10, padding: 20 }}>
      <JimboText size="title" tone="white">Title Text</JimboText>
      <JimboText size="header" tone="orange">Header Text (Orange)</JimboText>
      <JimboText size="lg" tone="white">Large Text</JimboText>
      <JimboText size="md" tone="white">Medium Text (White)</JimboText>
      <JimboText size="sm" tone="blue">Small Text (Blue)</JimboText>
      <JimboText size="xs" tone="red">Extra Small (Red)</JimboText>
      <JimboText size="micro" tone="tarot">Micro Text (Tarot)</JimboText>
    </div>
  ),
};

export const Buttons: StoryObj = {
  render: () => (
    <div className="j-panel" style={{ display: "flex", flexDirection: "column", gap: 10, padding: 20, width: 300 }}>
      <JimboButton tone="orange" size="lg">Large Orange Button</JimboButton>
      <JimboButton tone="blue" size="md">Medium Blue Button</JimboButton>
      <JimboButton tone="red" size="sm">Small Red Button</JimboButton>
      <JimboButton tone="green" size="xs">XS Green Button</JimboButton>
      <div style={{ display: "flex", gap: 10 }}>
        <JimboButton tone="tarot" size="md" style={{ flex: 1 }}>Flex 1</JimboButton>
        <JimboButton tone="planet" size="md" style={{ flex: 1 }}>Flex 2</JimboButton>
      </div>
    </div>
  ),
};
