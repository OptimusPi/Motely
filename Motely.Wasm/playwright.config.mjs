import { defineConfig } from "@playwright/test";

export default defineConfig({
  testDir: "./tests-ui",
  testMatch: /.*\.spec\.mjs/,
  use: { baseURL: "http://127.0.0.1:4173" },
  webServer: {
    command: "node testui/serve.mjs",
    port: 4173,
    reuseExistingServer: true,
  },
});
