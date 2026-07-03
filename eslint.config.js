// For more info, see https://github.com/storybookjs/eslint-plugin-storybook#configuration-flat-config-format
import storybook from "eslint-plugin-storybook";

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
      'jaml-design/no-raw-button': 'off',
      'jaml-design/no-emoji-jsx': 'off',
      'jaml-design/no-uppercase-text': 'off',
      'jaml-design/no-bold-style': 'off',
    },
  },
  storybook.configs["flat/recommended"]
);
