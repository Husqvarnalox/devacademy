import { pickKeys, omitKeys, merge, type Mutable, type ElementOf, type PickByType, type Nullable, type Resolved } from "../src/transform.ts";

type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;
type Expect<T extends true> = T;

interface User { readonly id: number; name: string; age: number; email?: string }
declare const user: User;

// Отображаемые типы.
export type M1 = Expect<Equal<Mutable<{ readonly a: number; readonly b?: string }>, { a: number; b?: string }>>;
export type N1 = Expect<Equal<Nullable<{ a: string; b: number }>, { a: string | null; b: number | null }>>;
export type P1 = Expect<Equal<PickByType<{ a: string; b: number; c: string }, string>, { a: string; c: string }>>;
export type P2 = Expect<Equal<PickByType<{ a: string; b: number }, boolean>, {}>>;

// Условные типы.
export type E1 = Expect<Equal<ElementOf<string[]>, string>>;
export type E2 = Expect<Equal<ElementOf<readonly number[]>, number>>;
export type E3 = Expect<Equal<ElementOf<readonly [1, "a"]>, 1 | "a">>;
export type E4 = Expect<Equal<ElementOf<number>, never>>;
export type E5 = Expect<Equal<ElementOf<string[] | number[]>, string | number>>;

export type R1 = Expect<Equal<Resolved<() => Promise<number>>, number>>;
export type R2 = Expect<Equal<Resolved<(x: string) => string>, string>>;
export type R3 = Expect<Equal<Resolved<() => Promise<Promise<boolean>>>, boolean>>;

// Функции: результат использует Pick/Omit от типа входа.
export const picked = pickKeys(user, ["name", "age"]);
export const omitted = omitKeys(user, ["id", "email"]);
export type F1 = Expect<Equal<typeof picked, Pick<User, "name" | "age">>>;
export type F2 = Expect<Equal<typeof omitted, Omit<User, "id" | "email">>>;
// @ts-expect-error у User нет поля phone
pickKeys(user, ["phone"]);
// @ts-expect-error у User нет поля phone
omitKeys(user, ["phone"]);

export const merged = merge(user, { age: 40 });
export type F3 = Expect<Equal<typeof merged, User>>;
// @ts-expect-error возраст - число
merge(user, { age: "сорок" });
// @ts-expect-error лишнего поля нет в User
merge(user, { phone: "1" });
