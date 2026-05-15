import type { Meta, StoryObj } from '@storybook/react';
import React from 'react';

// Import all missing components
import { JamlCodeEditor } from './JamlCodeEditor';
import { JamlCurator } from './JamlCurator';
import { JamlIdeToolbar } from './JamlIdeToolbar';
import { JamlIdeVisual } from './JamlIdeVisual';
import { JamlMapPreview } from './JamlMapPreview';
import { Jamlyzer } from './Jamlyzer';
import { MotelyVersionBadge } from './MotelyVersionBadge';
import { PaginatedFilterBrowser } from './PaginatedFilterBrowser';
import { RunConfigModal } from './RunConfigModal';
import { RealStandardcard as Standardcard } from './Standardcard';
import { AnalyzerExplorer } from './AnalyzerExplorer';
import { CardFan } from './CardFan';
import { CardList } from './CardList';

// Import missing from ui/
import { JimboFloating } from '../ui/JimboFloating';
import { PanelSplitter } from '../ui/PanelSplitter';
import { JimboBalatroFooter } from '../ui/footer';
import { JimboFilterBar } from '../ui/jimboFilterBar';

const meta = {
  title: 'JAML / Missing Components',
  parameters: { layout: 'centered' },
} satisfies Meta;

export default meta;

const mockProps: any = {
  value: '',
  onChange: () => {},
  onSelect: () => {},
  items: [],
  cards: [],
  open: true,
  onClose: () => {},
  config: {},
  seed: 'WEEJOKER',
  version: '0.1.0'
};

export const _JamlCodeEditor: StoryObj = { render: () => <JamlCodeEditor {...mockProps} /> };
export const _JamlCurator: StoryObj = { render: () => <JamlCurator {...mockProps} /> };
export const _JamlIdeToolbar: StoryObj = { render: () => <JamlIdeToolbar {...mockProps} /> };
export const _JamlIdeVisual: StoryObj = { render: () => <JamlIdeVisual {...mockProps} /> };
export const _JamlMapPreview: StoryObj = { render: () => <JamlMapPreview {...mockProps} /> };
export const _Jamlyzer: StoryObj = { render: () => <Jamlyzer {...mockProps} /> };
export const _MotelyVersionBadge: StoryObj = { render: () => <MotelyVersionBadge {...mockProps} /> };
export const _PaginatedFilterBrowser: StoryObj = { render: () => <PaginatedFilterBrowser {...mockProps} /> };
export const _RunConfigModal: StoryObj = { render: () => <RunConfigModal {...mockProps} /> };
export const _Standardcard: StoryObj = { render: () => <Standardcard {...mockProps} /> };
export const _AnalyzerExplorer: StoryObj = { render: () => <AnalyzerExplorer {...mockProps} /> };
export const _CardFan: StoryObj = { render: () => <CardFan {...mockProps} /> };
export const _CardList: StoryObj = { render: () => <CardList {...mockProps} /> };

export const _JimboFloating: StoryObj = { render: () => <JimboFloating {...mockProps} /> };
export const _PanelSplitter: StoryObj = { render: () => <PanelSplitter {...mockProps} /> };
export const _JimboBalatroFooter: StoryObj = { render: () => <JimboBalatroFooter {...mockProps} /> };
export const _JimboFilterBar: StoryObj = { render: () => <JimboFilterBar {...mockProps} /> };
