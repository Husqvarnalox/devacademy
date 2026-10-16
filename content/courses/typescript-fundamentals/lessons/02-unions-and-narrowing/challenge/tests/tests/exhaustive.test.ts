import { describe, it, expect } from "vitest";
import { spawnSync } from "node:child_process";
import { cpSync, mkdirSync, readFileSync, rmSync, writeFileSync } from "node:fs";
import { join } from "node:path";

// Мутационная проверка: в копию кода добавляется новый вариант State или Action, и tsc обязан сообщить об ошибке.
function tsc(project: string): string[] {
  const r = spawnSync(process.execPath, ["node_modules/typescript/bin/tsc", "--noEmit", "--pretty", "false", "-p", project], {
    encoding: "utf8",
    timeout: 40_000,
  });
  if (r.error) throw r.error;
  return `${r.stdout}${r.stderr}`.split("\n").filter((l) => l.trim().length > 0);
}

function mutate(name: string, marker: string, extra: string): string[] {
  const dir = `.cache/mutation-${name}`;
  rmSync(dir, { recursive: true, force: true });
  mkdirSync(dir, { recursive: true });
  cpSync("src", join(dir, "src"), { recursive: true });
  const config = JSON.parse(readFileSync("tsconfig.json", "utf8"));
  config.include = ["src/**/*.ts"];
  writeFileSync(join(dir, "tsconfig.json"), JSON.stringify(config));
  const file = join(dir, "src", "state.ts");
  const text = readFileSync(file, "utf8");
  expect(text.includes(marker), `в src/state.ts должна остаться строка «${marker}»`).toBe(true);
  writeFileSync(file, text.replace(marker, `${marker}\n  | ${extra}`));
  return tsc(join(dir, "tsconfig.json"));
}

describe("исчерпываемость", () => {
  it("новый вариант State ломает компиляцию", () => {
    expect(tsc("tsconfig.json"), "сначала исправьте ошибки типов").toEqual([]);
    const lines = mutate("state", "export type State =", '{ status: "paused"; since: number }');
    expect(lines.length, "summary должна разбирать State через switch с assertNever").toBeGreaterThan(0);
  });
  it("новый вариант Action ломает компиляцию", () => {
    expect(tsc("tsconfig.json"), "сначала исправьте ошибки типов").toEqual([]);
    const lines = mutate("action", "export type Action =", '{ type: "cancel" }');
    expect(lines.length, "reducer должен разбирать Action через switch с assertNever").toBeGreaterThan(0);
  });
});
