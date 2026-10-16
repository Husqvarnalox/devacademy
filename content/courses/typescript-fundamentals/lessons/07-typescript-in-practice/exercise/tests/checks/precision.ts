import { Status, isStatus, label, Counter } from "../src/status.ts";
import { nth, lastOf, head, withRetries, retriesOf, type Options } from "../src/list.ts";
import { clamp, mean, VERSION } from "../vendor/legacy-math.js";
import { clampAll, average, libVersion } from "../src/stats.ts";

type Equal<A, B> = (<T>() => T extends A ? 1 : 2) extends <T>() => T extends B ? 1 : 2 ? true : false;
type Expect<T extends true> = T;
declare const u: unknown;

// Status - объединение литералов, значения доступны как у объекта.
export type S1 = Expect<Equal<Status, "active" | "blocked" | "pending">>;
export const s: Status = "active";
// @ts-expect-error неизвестный статус
export const s2: Status = "deleted";
export const s3: Status = Status.Blocked;
export type S2 = Expect<Equal<typeof Status.Active, "active">>;
if (isStatus(u)) {
  const narrowed: Status = u;
  void narrowed;
}
export const text: string = label(Status.Pending);
// @ts-expect-error label принимает только Status
label("x");

export const c: number = new Counter(1).increment().value;

// noUncheckedIndexedAccess: undefined обязан быть в типе.
export type L1 = Expect<Equal<ReturnType<typeof nth<number>>, number | undefined>>;
export type L2 = Expect<Equal<ReturnType<typeof lastOf<string>>, string | undefined>>;
export const h: number = head([1, 2, 3]);
// @ts-expect-error пустой массив не подходит под непустой кортеж
head([]);
export type L3 = Expect<Equal<ReturnType<typeof head<string>>, string>>;

// exactOptionalPropertyTypes.
export const o: Options = withRetries({}, 2);
export const n: number = retriesOf(o);
// @ts-expect-error при exactOptionalPropertyTypes undefined нельзя присвоить необязательному полю
export const bad: Options = { retries: undefined };

// Типы JavaScript-библиотеки.
export type V1 = Expect<Equal<ReturnType<typeof clamp>, number>>;
export type V2 = Expect<Equal<ReturnType<typeof mean>, number>>;
export type V3 = Expect<Equal<typeof VERSION, string>>;
// @ts-expect-error clamp принимает числа
clamp("5", 1, 10);
// @ts-expect-error mean принимает массив чисел
mean("1,2,3");
export const cl: number[] = clampAll([1, 20], 0, 10);
export const av: number = average([1, 2]);
export const lv: string = libVersion();
