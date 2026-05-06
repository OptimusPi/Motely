import type { Preview } from '@storybook/react-vite'
import '../src/ui/jimbo.css'

const preview: Preview = {
  parameters: {
    layout: 'fullscreen',
    controls: {
      matchers: {
        color: /(background|color)$/i,
        date: /Date$/i,
      },
    },
    backgrounds: {
      default: 'balatro',
      values: [{ name: 'balatro', value: '#1b2526' }],
    },
  },
};

export default preview;
