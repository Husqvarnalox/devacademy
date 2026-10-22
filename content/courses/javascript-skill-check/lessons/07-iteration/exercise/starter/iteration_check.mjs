// Итерация и генераторы: Range и ленивый конвейер.

export class Range {
  // TODO: итерируемый объект: start, end (не включая), step; шаг 0 - RangeError
  constructor(start, end, step = 1) {
    this.items = [];
    for (let i = start; i < end; i += step) this.items.push(i);
  }
}

/** Бесконечный генератор start, start + 1, ... */
export function naturals(start = 0) {
  // TODO: это должен быть бесконечный генератор, а не конечный массив
  return [start, start + 1, start + 2, start + 3, start + 4];
}

/** Ленивое отображение: значения fn(item, index). */
export function map(iterable, fn) {
  // TODO: сделайте генератор (function*) вместо массива
  return Array.from(iterable, fn);
}

/** Ленивый фильтр: pred(item, index). */
export function filter(iterable, pred) {
  // TODO: сделайте генератор (function*)
  return Array.from(iterable).filter(pred);
}

/** Первые n элементов; не запрашивает лишний элемент и закрывает источник. */
export function take(iterable, n) {
  // TODO: сделайте генератор (function*)
  return Array.from(iterable).slice(0, n);
}
