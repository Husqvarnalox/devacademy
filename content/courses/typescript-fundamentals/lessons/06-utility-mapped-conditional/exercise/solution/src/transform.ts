export type Mutable<T> = { -readonly [K in keyof T]: T[K] };

export type ElementOf<T> = T extends readonly (infer U)[] ? U : never;

export type PickByType<T, V> = { [K in keyof T as T[K] extends V ? K : never]: T[K] };

export type Nullable<T> = { [K in keyof T]: T[K] | null };

export type Resolved<F extends (...args: never[]) => unknown> = Awaited<ReturnType<F>>;

export function pickKeys<T extends object, K extends keyof T>(obj: T, keys: readonly K[]): Pick<T, K> {
  const result = {} as Pick<T, K>;
  for (const key of keys) {
    if (key in obj) result[key] = obj[key];
  }
  return result;
}

export function omitKeys<T extends object, K extends keyof T>(obj: T, keys: readonly K[]): Omit<T, K> {
  const result: Partial<T> = { ...obj };
  for (const key of keys) {
    delete result[key];
  }
  return result as Omit<T, K>;
}

export function merge<T extends object>(base: T, patch: Partial<T>): T {
  const result = { ...base };
  for (const key of Object.keys(patch) as (keyof T)[]) {
    const value = patch[key];
    if (value !== undefined) result[key] = value as T[keyof T];
  }
  return result;
}
