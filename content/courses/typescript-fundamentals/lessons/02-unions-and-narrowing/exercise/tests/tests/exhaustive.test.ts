import { describe, it, expect } from "vitest";
import { spawnSync } from "node:child_process";
import { cpSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";

// Мутационная проверка исчерпываемости: в копию кода добавляется новый вариант Shape, и tsc обязан
// сообщить об ошибке там, где вариант не обработан. switch с `default: return 0` такую проверку не проходит.
function tsc(project: string): string[] {
  const r = spawnSync(process.execPath, ["node_modules/typescript/bin/tsc", "--noEmit", "--pretty", "false", "-p", project], {
    encoding: "utf8",
    timeout: 40_000,
  });
  if (r.error) throw r.error;
  return `${r.stdout}${r.stderr}`.split("\n").filter((l) => l.trim().length > 0);
}

describe("исчерпываемость", () => {
  it("новый вариант Shape ломает компиляцию, пока он не обработан", () => {
    const base = tsc("tsconfig.json");
    expect(base, "сначала исправьте ошибки типов").toEqual([]);

    const dir = ".cache/mutation";
    rmSync(dir, { recursive: true, force: true });
    mkdirSync(dir, { recursive: true });
    cpSync("src", join(dir, "src"), { recursive: true });
    const config = JSON.parse(readFileSync("tsconfig.json", "utf8"));
    config.include = ["src/**/*.ts"];
    writeFileSync(join(dir, "tsconfig.json"), JSON.stringify(config));

    let patched = false;
    for (const name of readdirSync(join(dir, "src"))) {
      const file = join(dir, "src", name);
      const text = readFileSync(file, "utf8");
      if (/export type Shape =/.test(text)) {
        writeFileSync(file, text.replace("export type Shape =", 'export type Shape = { kind: "triangle"; base: number; height: number }\n '));
        patched = true;
      }
    }
    expect(patched, "в src должен остаться export type Shape =").toBe(true);

    const lines = tsc(join(dir, "tsconfig.json"));
    expect(lines.length, "после добавления варианта tsc должен сообщить об ошибке: нужна проверка исчерпываемости через never").toBeGreaterThan(0);
  });
});
