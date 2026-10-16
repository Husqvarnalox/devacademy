import { readFileSync } from "node:fs";

type Shape =
  | { kind: "circle"; r: number }
  | { kind: "rect"; w: number; h: number };

function isNonNegativeNumber(x: unknown): x is number {
  return typeof x === "number" && x >= 0;
}

function parseShape(line: string): Shape | undefined {
  let value: unknown;
  try {
    value = JSON.parse(line);
  } catch {
    return undefined;
  }
  if (typeof value !== "object" || value === null || Array.isArray(value)) return undefined;
  const obj = value as Record<string, unknown>;
  if (obj.kind === "circle" && isNonNegativeNumber(obj.r)) {
    return { kind: "circle", r: obj.r };
  }
  if (obj.kind === "rect" && isNonNegativeNumber(obj.w) && isNonNegativeNumber(obj.h)) {
    return { kind: "rect", w: obj.w, h: obj.h };
  }
  return undefined;
}

function area(s: Shape): number {
  switch (s.kind) {
    case "circle":
      return Math.PI * s.r * s.r;
    case "rect":
      return s.w * s.h;
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
