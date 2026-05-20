import js from '@eslint/js';
import globals from 'globals';
import reactHooks from 'eslint-plugin-react-hooks';
import reactRefresh from 'eslint-plugin-react-refresh';
import tseslint from 'typescript-eslint';
import jamlDesign from './eslint-rules/jaml-design.js';

export default tseslint.config(
  { ignores: ['dist/**', 'storybook-static/**', 'node_modules/**', 'assets/**', '**/*.d.ts', 'examples/**', '.claude/', '.claude/**'] },
  {
    extends: [js.configs.recommended, ...tseslint.configs.recommended],
    files: ['**/*.{ts,tsx}'],
    languageOptions: {
    ecmaVersion: 2020,
    globals: globals.browser,
  },
  plugins: {
    'react-hooks': reactHooks,
    'react-refresh': reactRefresh,
    'jaml-design': jamlDesign,
  },
  rules: {
    ...reactHooks.configs.recommended.rules,
    'react-refresh/only-export-components': [
      'warn',
      { allowConstantExport: true },
    ],
    'jaml-design/no-raw-button': 'error',
    'jaml-design/no-emoji-jsx': 'error',
    'jaml-design/no-uppercase-text': 'error',
    'jaml-design/no-bold-style': 'error',
  },
  },
  {
    // src/ui/ is where the Jimbo primitives live — they're allowed to use
    // raw <button>/<input> because they ARE the primitives.
    files: ['src/ui/**/*.{ts,tsx}'],
    rules: {
      'jaml-design/no-raw-button': 'off',
    },
  },
  {
    // CLI/build scripts aren't UI — design rules don't apply.
    files: ['scripts/**/*.{ts,tsx}', 'demo/**/*.{ts,tsx}', 'vite.config.ts', '.storybook/**/*.{ts,tsx}'],
    rules: {
      'jaml-design/no-raw-button': 'off',
      'jaml-design/no-emoji-jsx': 'off',
      'jaml-design/no-uppercase-text': 'off',
      'jaml-design/no-bold-style': 'off',
    },
  },
  {
    // Stories often render small inline UI; keep design rules on but allow raw
    // elements when the story is specifically testing primitive behaviour.
    // Also allow ALL CAPS — seed strings are uppercase Balatro data.
    files: ['**/*.stories.{ts,tsx}'],
    rules: {
      'jaml-design/no-raw-button': 'off',
      'jaml-design/no-uppercase-text': 'off',
    },
  },
  {
    files: ['.storybook/**/*.{ts,tsx}'],
    rules: {
      'react-refresh/only-export-components': 'off',
    },
  },
);
