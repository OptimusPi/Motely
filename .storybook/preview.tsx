import type { Preview } from '@storybook/react-vite'
import React from 'react'
import '../src/ui/jimbo.css'
import './preview.css'
import { JimboBackground } from '../src/ui/jimboBackground'
import { JimboApp } from '../src/ui/jimboApp'

const preview: Preview = {
  parameters: {
    layout: 'centered',
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
      <>
        <JimboBackground />
        <JimboApp>
          <Story />
        </JimboApp>
      </>
    ),
  ],
};

export default preview;
