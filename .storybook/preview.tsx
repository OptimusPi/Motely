import type { Preview } from '@storybook/react-vite'
import React, { useEffect } from 'react'
import '../src/ui/jimbo.css'
import './preview.css'
import { JimboBackground } from '../src/ui/jimboBackground'
import { JimboApp } from '../src/ui/jimboApp'
import { ensureMotelyReady } from '../src/motelyBoot'

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
    (Story) => (
      <StorybookMotelyWarmup>
        <JimboBackground />
        <div className="sb-jimbo-stage">
          <div className="sb-jimbo-frame">
            <JimboApp className="sb-jimbo-app">
              <Story />
            </JimboApp>
          </div>
        </div>
      </StorybookMotelyWarmup>
    ),
  ],
};

export default preview;
