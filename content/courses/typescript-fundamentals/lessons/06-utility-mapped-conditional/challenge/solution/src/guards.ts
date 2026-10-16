export type Guard<T> = (v: unknown) => v is T;

export type Infer<G> = G extends Guard<infer T> ? T : never;

export function str(): Guard<string> {
  return (v): v is string => typeof v === "string";
}

export function num(): Guard<number> {
  return (v): v is number => typeof v === "number" && Number.isFinite(v);
}

export function bool(): Guard<boolean> {
  return (v): v is boolean => typeof v === "boolean";
}

export function literal<const L extends string | number | boolean>(value: L): Guard<L> {
  return (v): v is L => v === value;
}

export function arr<T>(item: Guard<T>): Guard<T[]> {
  return (v): v is T[] => Array.isArray(v) && v.every((x) => item(x));
}

export function optional<T>(g: Guard<T>): Guard<T | undefined> {
  return (v): v is T | undefined => v === undefined || g(v);
}

export function oneOf<const T extends readonly Guard<unknown>[]>(...variants: T): Guard<Infer<T[number]>> {
  return (v): v is Infer<T[number]> => variants.some((g) => g(v));
}

export function obj<S extends Record<string, Guard<unknown>>>(shape: S): Guard<{ [K in keyof S]: Infer<S[K]> }> {
  return (v): v is { [K in keyof S]: Infer<S[K]> } => {
    if (typeof v !== "object" || v === null || Array.isArray(v)) return false;
    return Object.entries(shape).every(([key, g]) => g(Object.hasOwn(v, key) ? (v as Record<string, unknown>)[key] : undefined));
  };
}
