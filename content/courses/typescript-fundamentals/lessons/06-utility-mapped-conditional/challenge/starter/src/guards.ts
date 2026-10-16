// Комбинаторы проверок: из маленьких Guard<T> собираются проверки сложных данных, а тип данных выводится из самой проверки.

export type Guard<T> = (v: unknown) => v is T;

// TODO: достать T из Guard<T> (условный тип с infer); для не-Guard - never
export type Infer<G> = unknown;

export function str(): Guard<string> {
  // TODO
  return (v): v is string => false;
}

export function num(): Guard<number> {
  // TODO: конечное число (NaN и Infinity не подходят)
  return (v): v is number => false;
}

export function bool(): Guard<boolean> {
  // TODO
  return (v): v is boolean => false;
}

// TODO: сделайте L точным литеральным типом (параметр типа с const) вместо string | number | boolean
export function literal(value: string | number | boolean): Guard<string | number | boolean> {
  return (v): v is string | number | boolean => false;
}

// TODO: сделайте функцию обобщённой: arr(str()) должен давать Guard<string[]>
export function arr(item: Guard<unknown>): Guard<unknown[]> {
  return (v): v is unknown[] => false;
}

// TODO: Guard<T | undefined>
export function optional(g: Guard<unknown>): Guard<unknown> {
  return (v): v is unknown => false;
}

// TODO: любой из вариантов; тип - объединение типов вариантов (Infer<T[number]>)
export function oneOf(...variants: Guard<unknown>[]): Guard<unknown> {
  return (v): v is unknown => false;
}

// TODO: объект с заданными полями; тип { [K in keyof S]: Infer<S[K]> }. Лишние поля в данных допустимы.
export function obj(shape: Record<string, Guard<unknown>>): Guard<Record<string, unknown>> {
  return (v): v is Record<string, unknown> => false;
}
