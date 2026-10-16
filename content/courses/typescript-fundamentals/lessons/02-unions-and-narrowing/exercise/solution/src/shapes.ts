export type Shape =
  | { kind: "circle"; r: number }
  | { kind: "rect"; w: number; h: number }
  | { kind: "square"; side: number };

export function assertNever(x: never): never {
  throw new Error(`Необработанный вариант: ${JSON.stringify(x)}`);
}

export function area(s: Shape): number {
  switch (s.kind) {
    case "circle":
      return Math.PI * s.r * s.r;
    case "rect":
      return s.w * s.h;
    case "square":
      return s.side * s.side;
    default:
      return assertNever(s);
  }
}

export function describe(s: Shape): string {
  switch (s.kind) {
    case "circle":
      return `круг r=${s.r}`;
    case "rect":
      return `прямоугольник ${s.w}x${s.h}`;
    case "square":
      return `квадрат ${s.side}`;
    default:
      return assertNever(s);
  }
}

export type Input = string | number | boolean | null | undefined | readonly string[];

export function formatInput(v: Input): string {
  if (v === null) return "null";
  if (v === undefined) return "нет значения";
  if (typeof v === "string") return `'${v}'`;
  if (typeof v === "number") return v.toFixed(2);
  if (typeof v === "boolean") return v ? "да" : "нет";
  return `[${v.join(", ")}]`;
}
