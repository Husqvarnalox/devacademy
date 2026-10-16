import { describe, it } from "vitest";
import { spawnSync } from "node:child_process";
import { readdirSync, readFileSync } from "node:fs";
import { join } from "node:path";

// Проверка типов - часть оценки. tsc запускается из пакета typescript окружения курсов с настройками из tsconfig*.json.
// Эти файлы лежат среди скрытых тестов и заменяют одноимённые файлы ученика.
const SUPPRESSION = /@ts-(nocheck|ignore|expect-error)/;

function sourceFiles(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((e) =>
    e.isDirectory() ? sourceFiles(join(dir, e.name)) : [join(dir, e.name)],
  );
}

function runTsc(project: string): string[] {
  const r = spawnSync(
    process.execPath,
    ["node_modules/typescript/bin/tsc", "--noEmit", "--pretty", "false", "-p", project],
    { encoding: "utf8", timeout: 40_000 },
  );
  if (r.error) throw r.error;
  return `${r.stdout}${r.stderr}`.split("\n").map((l) => l.trimEnd()).filter((l) => l.length > 0);
}

function fail(title: string, lines: string[]): never {
  const shown = lines.slice(0, 12).join("\n");
  const more = lines.length > 12 ? `\n... и ещё ${lines.length - 12} строк` : "";
  // Обычный объект вместо Error: в отчёте нужен только текст диагностики, без дублирования и без стека тестового файла.
  throw { name: "TypeCheckError", message: `${title}\n${shown}${more}` };
}

function forbidSuppressions(): void {
  for (const f of sourceFiles("src")) {
    if (f.endsWith(".ts") && SUPPRESSION.test(readFileSync(f, "utf8"))) {
      fail("Ошибки типов нужно исправлять, а не скрывать:", [`${f}: найден @ts-nocheck, @ts-ignore или @ts-expect-error`]);
    }
  }
}

describe("типы", () => {
  it("код проходит tsc --strict", () => {
    forbidSuppressions();
    const lines = runTsc("tsconfig.json");
    if (lines.length > 0) fail("tsc нашёл ошибки типов:", lines);
  });

  it("типы точные: проверки использования", () => {
    forbidSuppressions();
    const lines = runTsc("tsconfig.precision.json");
    if (lines.length > 0) fail("Типы слишком слабые или неверные (any, as, неточная сигнатура). tsc сообщил:", lines);
  });
});
