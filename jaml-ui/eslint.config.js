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
      // Design rules — the CI mirror of .claude/hooks/check-design.mjs. The hook
      // only guards Claude's Edit/Write inside a session; these guard everything
      // else (IDE edits, commits, other agents, CI). Source of truth for all of
      // them: CLAUDE.md "Design rules".
      'jaml-design/no-flex': 'error',
      'jaml-design/no-raw-html': 'error',
      'jaml-design/no-emoji-jsx': 'error',
      'jaml-design/no-uppercase-text': 'error',
      'jaml-design/no-bold-style': 'error',
      'jaml-design/no-inline-style': 'error',
      'jaml-design/no-token-in-jsx-style': 'error',
      'jaml-design/no-inline-component': 'error',
    },
  },
  // src/ui/ IS the primitive layer: JimboButton is *made of* a raw <button>, and
  // it styles itself inline. Stories exercise the primitives directly. Both are
  // exempt from the rules that exist to push consumers toward the primitives.
  // no-flex is NOT exempted anywhere — rule #1 has no exceptions.
  {
    files: ['src/ui/**/*.{ts,tsx}', '**/*.stories.{ts,tsx}'],
    rules: {
      'jaml-design/no-raw-html': 'off',
      'jaml-design/no-inline-style': 'off',
      'jaml-design/no-inline-component': 'off',
    },
  },
  storybook.configs["flat/recommended"]
);
