// Универсальные функции над коллекциями и тип Result.
// Сейчас вместо параметров типа стоит any - замените его обобщениями так, чтобы тип результата зависел от типа входа.

// TODO: добавьте для E значение по умолчанию Error: Result<number> должен значить Result<number, Error>
export type Result<T, E> = { ok: true; value: T } | { ok: false; error: E };

export function first(xs: readonly any[]): any {
  // TODO: первый элемент или undefined для пустого массива
  return xs;
}

export function pluck(items: readonly any[], key: string): any[] {
  // TODO: значения поля key у каждого элемента. Тип key должен быть ключом T (K extends keyof T), результат - T[K][]
  return [];
}

export function groupBy(items: readonly any[], keyOf: (item: any) => any): Map<any, any[]> {
  // TODO: сгруппировать элементы по ключу, сохраняя порядок первого появления ключей и порядок элементов
  return new Map();
}

export function ok(value: any): Result<any, never> {
  // TODO: успешный результат. Тип должен быть Result<T, never>
  return { ok: true, value };
}

export function err(error: any): Result<never, any> {
  // TODO: ошибка. Тип должен быть Result<never, E>
  return { ok: false, error };
}

export function mapResult(r: Result<any, any>, f: (value: any) => any): Result<any, any> {
  // TODO: применить f к значению успешного результата; ошибку вернуть как есть
  return r;
}

export function unwrapOr(r: Result<any, any>, fallback: any): any {
  // TODO: значение при ok, иначе fallback
  return fallback;
}
