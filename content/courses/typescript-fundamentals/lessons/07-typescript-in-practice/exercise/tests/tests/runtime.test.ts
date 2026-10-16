import { describe, it, expect } from "vitest";
import { spawnSync } from "node:child_process";

describe("запуск без сборки", () => {
  it("Node выполняет код напрямую (strip-types): нет enum, namespace и свойств-параметров", () => {
    const r = spawnSync(
      process.execPath,
      ["--experimental-strip-types", "--disable-warning=ExperimentalWarning", "smoke/smoke.ts"],
      { encoding: "utf8", timeout: 30_000 },
    );
    if (r.status !== 0) {
      const firstLine = `${r.stderr}`.split("\n").find((l) => /Error|error/.test(l)) ?? r.stderr;
      throw { name: "StripTypesError", message: `Node не смог запустить код без сборки:\n${firstLine}` };
    }
    expect(JSON.parse(r.stdout)).toEqual({
      statuses: ["active", "blocked", "pending"],
      is: [true, false, false],
      label: "Заблокирован",
      counter: 2,
      list: [2, null, null, "a"],
      retries: [3, 0, false],
      stats: [[0, 5, 10], 3, "1.2.0"],
    });
  });
});
