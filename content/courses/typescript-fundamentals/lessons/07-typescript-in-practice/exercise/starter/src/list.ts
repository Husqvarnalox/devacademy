// Работа со списками при включённых noUncheckedIndexedAccess и exactOptionalPropertyTypes.

export interface Options {
  retries?: number;
}

// TODO: чтение по индексу может вернуть undefined - отразите это в типе результата
export function nth<T>(xs: readonly T[], i: number): T {
  return xs[i];
}

// TODO: последний элемент или undefined для пустого массива
export function lastOf<T>(xs: readonly T[]): T {
  return xs[xs.length - 1];
}

// TODO: непустой кортеж [T, ...T[]] гарантирует первый элемент, поэтому undefined в результате не нужен
export function head<T>(xs: readonly T[]): T {
  return xs[0];
}

export function withRetries(o: Options, n: number | undefined): Options {
  // TODO: при n === undefined у результата не должно быть ключа retries вообще (exactOptionalPropertyTypes запрещает retries: undefined)
  return { ...o, retries: n };
}

export function retriesOf(o: Options): number {
  // TODO: retries или 3 по умолчанию (0 - допустимое значение)
  return 3;
}
