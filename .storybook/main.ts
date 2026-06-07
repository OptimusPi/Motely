import type { StorybookConfig } from '@storybook/react-vite';

const config: StorybookConfig = {
  "stories": [
    "../src/**/*.mdx",
    "../src/**/*.stories.@(js|jsx|mjs|ts|tsx)"
  ],
  "addons": [
    "@chromatic-com/storybook",
    "@storybook/addon-vitest",
    "@storybook/addon-a11y",
    "@storybook/addon-docs",
    "@storybook/addon-mcp"
  ],
  "framework": "@storybook/react-vite",
  // Let the Vite dev server answer to any Host header — required when reaching
  // Storybook through a Cloudflare tunnel / custom domain (e.g. *.8pi.me).
  // Without this, Vite rejects the tunnel host and the browser downloads the
  // "Invalid Host" response as document.txt instead of rendering.
  viteFinal: async (viteConfig) => {
    viteConfig.server ??= {};
    viteConfig.server.allowedHosts = true;
    return viteConfig;
  }
};
export default config;