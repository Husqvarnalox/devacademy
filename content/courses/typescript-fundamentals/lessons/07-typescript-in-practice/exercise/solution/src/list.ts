export interface Options {
  retries?: number;
}

export function nth<T>(xs: readonly T[], i: number): T | undefined {
  return xs[i];
}

export function lastOf<T>(xs: readonly T[]): T | undefined {
  return xs[xs.length - 1];
}

export function head<T>(xs: readonly [T, ...T[]]): T {
  return xs[0];
}

export function withRetries(o: Options, n: number | undefined): Options {
  const { retries: _old, ...rest } = o;
  return n === undefined ? rest : { ...rest, retries: n };
}

export function retriesOf(o: Options): number {
  return o.retries ?? 3;
}
