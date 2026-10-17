import { defineConfig } from "vitest/config";

// Приложение Express проверяется без сети: запросы подаются прямо в функцию-приложение через light-my-request.
// Кэш Vite лежит в рабочем каталоге: node_modules доступен только для чтения.
export default defineConfig({
  cacheDir: ".cache/vite",
  test: {
    environment: "node",
    fsModuleCache: false, // кэш преобразованных модулей по умолчанию пишется в node_modules, а он доступен только для чтения
    include: ["tests/**/*.test.mjs"],
  },
});
