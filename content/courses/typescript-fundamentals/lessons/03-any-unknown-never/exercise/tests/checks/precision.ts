import { isRecord, isStringArray, parseUser, parseUserJson, describeError, expectUser, fail, type User } from "../src/parse.ts";

declare const u: unknown;

// Параметры границ принимают unknown: любой вход должен компилироваться.
export const r1 = parseUser(1);
export const r2 = parseUser(null);
export const r3 = parseUser({ id: 1 });
export const r4 = parseUserJson("{}");
export const m1: string = describeError(new Error("x"));
export const m2: string = describeError(42);
export const m3: User = expectUser(u);

// Результат - дискриминированное объединение: без проверки ok читать value нельзя.
// @ts-expect-error value есть только при ok === true
export const bad1 = r1.value;
// @ts-expect-error error есть только при ok === false
export const bad2 = r1.error;
if (r1.ok) {
  const id: number = r1.value.id;
  const email: string | null = r1.value.email;
  const tags: string[] = r1.value.tags;
  // @ts-expect-error id - число, а не строка
  const wrong: string = r1.value.id;
  void [id, email, tags, wrong];
} else {
  const e: string = r1.error;
  void e;
}

// Предикаты сужают до точного типа, а не до any.
if (isRecord(u)) {
  const field: unknown = u["a"];
  // @ts-expect-error значение поля - unknown, а не string
  const s: string = u["a"];
  void [field, s];
}
if (isStringArray(u)) {
  const a: string[] = u;
  // @ts-expect-error элементы - строки, а не числа
  const n: number = u[0];
  void [a, n];
}

// fail возвращает never: код после вызова недостижим, а результат подходит любому типу.
export function never1(): string {
  return fail("x");
}
