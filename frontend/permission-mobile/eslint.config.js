import js from '@eslint/js'

export default [
  {
    ignores: ['**/node_modules/**', '**/dist/**', '**/*.ts', '**/*.tsx', '**/*.vue'],
  },
  {
    files: ['**/*.js', '**/*.mjs', '**/*.cjs'],
    ...js.configs.recommended,
    languageOptions: {
      globals: {
        URL: 'readonly',
        Response: 'readonly',
        caches: 'readonly',
        fetch: 'readonly',
        self: 'readonly',
      },
    },
  },
]
