import { distance, centroid, translate, nearest } from "../src/geometry.ts";

const origin = { x: 0, y: 0 };
const labelled = { x: 3, y: 4, label: "A" };

// Структурная совместимость: лишние свойства допустимы, если это не свежий литерал.
export const d1: number = distance(origin, labelled);

// @ts-expect-error свежий литерал с лишним свойством label
distance({ x: 1, y: 2, label: "B" }, origin);
// @ts-expect-error строка вместо числа в координате
distance({ x: "1", y: 2 }, origin);
// @ts-expect-error не хватает свойства y
distance({ x: 1 }, origin);
// @ts-expect-error результат distance - number, а не string
export const bad1: string = distance(origin, origin);

// Только для чтения: функция не должна требовать изменяемый массив.
const frozen: readonly { x: number; y: number }[] = [origin, labelled];
export const c = centroid(frozen);
// @ts-expect-error centroid может вернуть undefined
export const cx: number = c.x;
export const cOk: number | undefined = c?.x;

// @ts-expect-error translate возвращает точку, а не строку
export const bad2: string = translate(origin, 1, 1);
// @ts-expect-error dx должен быть числом
translate(origin, "1", 1);

export const near = nearest(frozen, origin);
// @ts-expect-error nearest может вернуть undefined
export const nx: number = near.x;
