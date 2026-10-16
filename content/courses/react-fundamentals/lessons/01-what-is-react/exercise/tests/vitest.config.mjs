import { defineConfig } from "vitest/config";

// JSX компилируется встроенным oxc (автоматический runtime): плагины не нужны.
// Кэш Vite лежит в рабочем каталоге: node_modules доступен только для чтения.
export default defineConfig({
  cacheDir: ".cache/vite",
  oxc: { jsx: { runtime: "automatic" } },
  test: {
    environment: "jsdom",
    globals: true, // нужно Testing Library для автоматической очистки DOM между тестами
    include: ["tests/**/*.test.jsx"],
  },
});
