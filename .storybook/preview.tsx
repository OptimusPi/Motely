import type { Preview } from '@storybook/react-vite'
import React from 'react'
import '../src/ui/jimbo.css'
import './preview.css'
import { JimboBackground } from '../src/ui/jimboBackground'
import { JimboApp } from '../src/ui/jimboApp'
<<<<<<< HEAD
import { ensureMotelyReady } from '../src/lib/motely/runtime'

await ensureMotelyReady();
=======
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45

const preview: Preview = {
  parameters: {
    layout: 'fullscreen',
<<<<<<< HEAD
=======

>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
<<<<<<< HEAD
    backgrounds: { disable: true },
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
          {useHarness ? (
            <JimboApp fluid={jimboHarness === 'fluid'}>
              {content}
            </JimboApp>
          ) : (
            content
          )}
        </>
      );
    },
=======

    backgrounds: { disable: true },

    a11y: {
      // 'todo' - show a11y violations in the test UI only
      // 'error' - fail CI on a11y violations
      // 'off' - skip a11y checks entirely
      test: 'todo'
    }
  },
  decorators: [
    // One shell, always. The hard-locked 320×568 JimboApp is THE container —
    // no opt-out, no harness toggle. Centered in the canvas with no flex via
    // the position:fixed + inset:0 + margin:auto trick (see .sb-stage); the
    // .j-app (540) and footer (28) stack by normal block flow inside it.
    (Story) => (
      <>
        <JimboBackground />
        <div className="sb-stage">
          <JimboApp>
            <Story />
          </JimboApp>
        </div>
      </>
    ),
>>>>>>> 4c1c0b639ac307d7366dccd1170ebadffbc2ab45
  ],
};

export default preview;
