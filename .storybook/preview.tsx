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
        <div style={{ display: 'flex', justifyContent: 'center', alignItems: 'flex-start', minHeight: '100vh', padding: '20px', boxSizing: 'border-box', overflow: 'auto' }}>
          <div style={{ position: 'relative', width: 375, height: 667, flexShrink: 0, boxShadow: '0 0 20px rgba(0,0,0,0.5)', borderRadius: 12, overflow: 'hidden' }}>
            <JimboApp style={{ margin: 0, height: '100%' }}>
              <Story />
            </JimboApp>
          </div>
        </div>
      </StorybookMotelyWarmup>
    ),
  ],
};

export default preview;
