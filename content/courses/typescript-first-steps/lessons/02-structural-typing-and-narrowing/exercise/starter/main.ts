import { readFileSync } from "node:fs";

type Shape =
  | { kind: "circle"; r: number }
  | { kind: "rect"; w: number; h: number };

function parseShape(line: string): Shape | undefined {
  let value: unknown;
  try {
    value = JSON.parse(line);
  } catch {
    return undefined;
  }
  // TODO: проверьте форму value и верните Shape (или undefined для недопустимых данных)
  return undefined;
}

function area(s: Shape): number {
  switch (s.kind) {
    case "circle":
      // TODO: площадь круга
      return 0;
    // TODO: случай "rect"
    default: {
      const unreachable: never = s;
      return unreachable;
    }
  }
}

const lines = readFileSync(0, "utf8").split("\n");
const n = Number(lines[0]);
for (let i = 1; i <= n; i++) {
  const shape = parseShape(lines[i] ?? "");
  console.log(shape === undefined ? "invalid" : `${shape.kind} ${area(shape).toFixed(2)}`);
}
