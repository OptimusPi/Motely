import type { Preview } from '@storybook/react-vite'
import React, { useEffect } from 'react'
import '../src/ui/jimbo.css'
import './preview.css'
import { JimboBackground } from '../src/ui/jimboBackground'
import { JimboApp } from '../src/ui/jimboApp'
import bootsharp from 'motely-wasm'

async function ensureMotelyReady(): Promise<void> {
  if (bootsharp.getStatus() === bootsharp.BootStatus.Standby) {
    await bootsharp.boot('/motely-wasm/bin')
  }
}

function StorybookMotelyWarmup({ children }: { children: React.ReactNode }) {
  useEffect(() => {
    void ensureMotelyReady().catch(() => {
      /* Boot errors surface via components that call ensureMotelyReady again */
    })
  }, [])
  return children
}

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

      if (jimboHarness === false) {
        return content;
      }

      return (
        <StorybookMotelyWarmup>
          <JimboBackground />
          <JimboApp fluid={jimboHarness === 'fluid'}>
             {content}
          </JimboApp>
        </StorybookMotelyWarmup>
      );
    },
  ],
};

export default preview;
