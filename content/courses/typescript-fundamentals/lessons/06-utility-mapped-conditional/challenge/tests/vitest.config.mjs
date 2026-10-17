import { defineConfig } from "vitest/config";

// Vitest сам стирает типы (oxc) и ничего не проверяет: проверку типов выполняет tests/typecheck.test.ts,
// который запускает tsc из окружения курсов. Кэш Vite лежит в рабочем каталоге: node_modules доступен только для чтения.
export default defineConfig({
  cacheDir: ".cache/vite",
  test: {
    fsModuleCache: false, // кэш преобразованных модулей по умолчанию пишется в node_modules, а он доступен только для чтения
    environment: "node",
    include: ["tests/**/*.test.ts"],
    testTimeout: 30000,
  },
});
