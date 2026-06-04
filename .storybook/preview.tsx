import type { Preview } from '@storybook/react-vite'
import React from 'react'
import '../src/ui/jimbo.css'
import './preview.css'
import { JimboBackground } from '../src/ui/jimboBackground'
import { JimboApp } from '../src/ui/jimboApp'
import { ensureMotelyReady } from '../src/lib/motely/runtime'

await ensureMotelyReady();

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

    a11y: {
      // 'todo' - show a11y violations in the test UI only
      // 'error' - fail CI on a11y violations
      // 'off' - skip a11y checks entirely
      test: 'todo'
    }
  },
  decorators: [
    (Story, { parameters }) => {
      const { jimboHarness, jimboBackground } = parameters;
      const content = <Story />;
      const useHarness = jimboHarness !== false;
      const showBackground = jimboBackground !== false;

      return (
        <>
          {showBackground ? <JimboBackground /> : null}
          {useHarness ? <JimboApp>{content}</JimboApp> : content}
        </>
      );
    },
  ],
};

export default preview;
