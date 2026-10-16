// Утилитарные, отображаемые и условные типы + функции, которые на них опираются.
// Сейчас типы-заглушки не делают ничего полезного: замените их, а в функциях уберите any.

// TODO: убрать readonly у всех полей: { -readonly [K in keyof T]: T[K] }
export type Mutable<T> = T;

// TODO: тип элемента массива (в том числе readonly и кортежа), для не массива - never. Подсказка: условный тип и infer
export type ElementOf<T> = unknown;

// TODO: оставить только те поля, значения которых подходят под V. Подсказка: ключи через as в отображаемом типе
export type PickByType<T, V> = T;

// TODO: у каждого поля разрешить ещё и null
export type Nullable<T> = T;

// TODO: тип значения, которым завершается async-функция F: Awaited<ReturnType<F>>
export type Resolved<F extends (...args: never[]) => unknown> = unknown;

export function pickKeys(obj: any, keys: readonly string[]): any {
  // TODO: новый объект только с указанными ключами, которые есть в obj. Тип результата - Pick<T, K>
  return obj;
}

export function omitKeys(obj: any, keys: readonly string[]): any {
  // TODO: копия без указанных ключей. Тип результата - Omit<T, K>
  return obj;
}

export function merge(base: any, patch: any): any {
  // TODO: новый объект: поля base, поверх них поля patch, значение которых не undefined. Тип patch - Partial<T>
  return base;
}
