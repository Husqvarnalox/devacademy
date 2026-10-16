import { first, pluck, groupBy, ok, err, mapResult, unwrapOr, type Result } from "../src/collections.ts";

type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;
type Expect<T extends true> = T;

interface User { name: string; age: number; admin: boolean }
declare const users: readonly User[];

// first: тип результата следует за типом элементов.
export const f1 = first([1, 2, 3]);
export const f2 = first(["a"]);
export const f3 = first(users);
export type T1 = Expect<Equal<typeof f1, number | undefined>>;
export type T2 = Expect<Equal<typeof f2, string | undefined>>;
export type T3 = Expect<Equal<typeof f3, User | undefined>>;

// pluck: ключ ограничен keyof T, результат - T[K][].
export const names = pluck(users, "name");
export const ages = pluck(users, "age");
export type T4 = Expect<Equal<typeof names, string[]>>;
export type T5 = Expect<Equal<typeof ages, number[]>>;
// @ts-expect-error у User нет поля email
pluck(users, "email");

// groupBy: тип ключа берётся из функции.
export const byAge = groupBy(users, (u) => u.age);
export const byAdmin = groupBy(users, (u) => u.admin);
export type T6 = Expect<Equal<typeof byAge, Map<number, User[]>>>;
export type T7 = Expect<Equal<typeof byAdmin, Map<boolean, User[]>>>;

// Result: значение по умолчанию для E и вывод параметров.
export const a = ok(1);
export const b = err("плохо");
export type T8 = Expect<Equal<typeof a, Result<number, never>>>;
export type T9 = Expect<Equal<typeof b, Result<never, string>>>;
export const r1: Result<number> = err(new Error("x"));
// @ts-expect-error по умолчанию ошибка - Error, а не string
export const r2: Result<number> = err("текст");

export const m = mapResult(a, (n) => String(n));
export type T10 = Expect<Equal<typeof m, Result<string, never>>>;
export const m2 = mapResult(b, (n: number) => n + 1);
export type T11 = Expect<Equal<typeof m2, Result<number, string>>>;

// unwrapOr: запасное значение должно подходить к типу значения.
export const u1 = unwrapOr(a, 0);
export type T12 = Expect<Equal<typeof u1, number>>;
// @ts-expect-error запасное значение другого типа
unwrapOr(a, "ноль");
