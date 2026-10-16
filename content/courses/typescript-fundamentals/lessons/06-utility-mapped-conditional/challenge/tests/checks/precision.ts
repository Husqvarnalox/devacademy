import { str, num, bool, literal, arr, optional, oneOf, obj, type Guard, type Infer } from "../src/guards.ts";

type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;
type Expect<T extends true> = T;
declare const u: unknown;

export type I1 = Expect<Equal<Infer<Guard<number>>, number>>;
export type I2 = Expect<Equal<Infer<string>, never>>;
export type I3 = Expect<Equal<Infer<ReturnType<typeof str>>, string>>;

export const gNum = num();
export const gArr = arr(str());
export const gOpt = optional(bool());
export const gLit = literal("admin");
export const gOne = oneOf(literal("admin"), literal("user"), num());
export type G1 = Expect<Equal<typeof gNum, Guard<number>>>;
export type G2 = Expect<Equal<typeof gArr, Guard<string[]>>>;
export type G3 = Expect<Equal<typeof gOpt, Guard<boolean | undefined>>>;
export type G4 = Expect<Equal<typeof gLit, Guard<"admin">>>;
export type G5 = Expect<Equal<Infer<typeof gOne>, "admin" | "user" | number>>;

export const userGuard = obj({
  id: num(),
  name: str(),
  tags: arr(str()),
  role: oneOf(literal("admin"), literal("user")),
  nick: optional(str()),
  address: obj({ city: str(), zip: optional(num()) }),
});
export type User = Infer<typeof userGuard>;
export type U1 = Expect<
  Equal<
    User,
    {
      id: number;
      name: string;
      tags: string[];
      role: "admin" | "user";
      nick: string | undefined;
      address: { city: string; zip: number | undefined };
    }
  >
>;

if (userGuard(u)) {
  const n: number = u.id;
  const tagLengths: number[] = u.tags.map((t) => t.length);
  const city: string = u.address.city;
  // @ts-expect-error id - число, а не строка
  u.id.toUpperCase();
  // @ts-expect-error role - только admin или user
  const bad: "guest" = u.role;
  void [n, tagLengths, city, bad];
}

// @ts-expect-error arr принимает Guard, а не значение
arr(5);
// @ts-expect-error в obj значениями должны быть Guard
obj({ id: 5 });
