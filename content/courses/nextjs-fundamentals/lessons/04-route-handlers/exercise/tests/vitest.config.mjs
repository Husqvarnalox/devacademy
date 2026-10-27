import { defineConfig } from "vitest/config";

// TSX компилируется встроенным oxc (автоматический runtime), типы стираются без проверки.
// Кэш Vite лежит в рабочем каталоге: node_modules доступен только для чтения.
export default defineConfig({
  cacheDir: ".cache/vite",
  oxc: { jsx: { runtime: "automatic" } },
  test: {
    fsModuleCache: false, // кэш преобразованных модулей по умолчанию пишется в node_modules, а он доступен только для чтения
    environment: "jsdom",
    globals: true, // нужно Testing Library для автоматической очистки DOM между тестами
    include: ["tests/**/*.test.{ts,tsx}"],
  },
});
