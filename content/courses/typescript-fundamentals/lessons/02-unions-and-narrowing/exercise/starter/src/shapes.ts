// Фигуры и значения-объединения. Реализуйте функции так, чтобы код проходил tsc --strict.

export type Shape =
  | { kind: "circle"; r: number }
  | { kind: "rect"; w: number; h: number }
  | { kind: "square"; side: number };

/** Сюда можно прийти, только если switch разобрал все варианты. */
export function assertNever(x: never): never {
  throw new Error(`Необработанный вариант: ${JSON.stringify(x)}`);
}

export function area(s: Shape): number {
  // TODO: switch (s.kind) с веткой на каждый вид; в default вызовите assertNever(s)
  return 0;
}

export function describe(s: Shape): string {
  // TODO: "круг r=2", "прямоугольник 3x4", "квадрат 5"
  return "";
}

export type Input = string | number | boolean | null | undefined | readonly string[];

export function formatInput(v: Input): string {
  // TODO: string -> 'текст' в одинарных кавычках; number -> toFixed(2); boolean -> "да" / "нет";
  //       null -> "null"; undefined -> "нет значения"; массив строк -> "[a, b]"
  return "";
}
