// Обобщения. Слово export делает объявление видимым для проверки.

// Первый элемент списка или undefined для пустого списка.
export function first<T>(items: readonly T[]): T | undefined {
  // TODO
  return items[items.length];
}

// Значения поля key у всех элементов: pluck([{ a: 1 }, { a: 2 }], "a") -> [1, 2].
// Тип результата должен соответствовать типу выбранного поля.
export function pluck<T, K extends keyof T>(items: readonly T[], key: K): T[K][] {
  // TODO
  return [];
}

// Группирует элементы по ключу, который возвращает keyOf. Ключи идут в порядке первого появления,
// внутри группы порядок элементов сохраняется.
export function groupBy<T, K>(items: readonly T[], keyOf: (item: T) => K): Map<K, T[]> {
  // TODO
  return new Map<K, T[]>();
}

// Стек: push добавляет, pop снимает последний (для пустого - undefined),
// peek показывает последний, не снимая (для пустого - undefined), size - число элементов.
export class Stack<T> {
  #items: T[] = [];

  push(item: T): void {
    // TODO
  }

  pop(): T | undefined {
    // TODO
    return undefined;
  }

  peek(): T | undefined {
    // TODO
    return undefined;
  }

  get size(): number {
    // TODO
    return -1;
  }
}
