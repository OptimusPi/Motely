import type { Preview } from '@storybook/react-vite'
import React from 'react'
import '../src/ui/jimbo.css'
import './preview.css'
import { JimboBackground } from '../src/ui/jimboBackground'
import { JimboApp } from '../src/ui/jimboApp'
import { MotelyProvider } from '../src/providers/MotelyProvider'

const preview: Preview = {
  parameters: {
    layout: 'fullscreen',
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    backgrounds: { disable: true },
  },
  decorators: [
    (Story, { parameters }) => {
      const { jimboHarness } = parameters;
      const content = <Story />;
      const useHarness = jimboHarness !== false;

      return (
        <MotelyProvider>
          <JimboBackground />
          {useHarness ? (
            <JimboApp fluid={jimboHarness === 'fluid'}>
              {content}
            </JimboApp>
          ) : (
            content
          )}
        </MotelyProvider>
      );
    },
  ],
};

export default preview;
