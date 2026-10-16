import { area, describe, formatInput, assertNever, type Shape } from "../src/shapes.ts";

export const a1: number = area({ kind: "circle", r: 1 });
export const a2: number = area({ kind: "rect", w: 1, h: 2 });
export const a3: number = area({ kind: "square", side: 3 });
export const d1: string = describe({ kind: "square", side: 3 });

// @ts-expect-error неизвестный вид фигуры
area({ kind: "hexagon", side: 1 });
// @ts-expect-error у круга нет свойства w
area({ kind: "circle", w: 1 });
// @ts-expect-error нет обязательного свойства h
area({ kind: "rect", w: 1 });
// @ts-expect-error area возвращает number, а не string
export const bad1: string = area({ kind: "square", side: 1 });
// @ts-expect-error describe возвращает string, а не number
export const bad2: number = describe({ kind: "square", side: 1 });

// assertNever принимает только never.
// @ts-expect-error строка не never
assertNever("x");

// Shape остаётся настоящим дискриминированным объединением: сужение работает, а общего r нет.
export function radiusOrZero(s: Shape): number {
  if (s.kind === "circle") {
    const r: number = s.r;
    return r;
  }
  // @ts-expect-error после исключения circle свойства r у фигуры нет
  return s.r;
}

export const f1: string = formatInput("текст");
export const f2: string = formatInput(1.5);
export const f3: string = formatInput(null);
export const f4: string = formatInput(undefined);
export const f5: string = formatInput(["a", "b"]);
export const f6: string = formatInput(true);
// @ts-expect-error объект не входит в Input
formatInput({});
// @ts-expect-error Date не входит в Input
formatInput(new Date());
// @ts-expect-error массив чисел не входит в Input
formatInput([1, 2]);
// @ts-expect-error formatInput возвращает string
export const bad3: number = formatInput(1);
