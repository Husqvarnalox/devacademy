// Массивы и итерация: сортировка, подсчёт, ленивые порции, деструктуризация с умолчаниями.

/** Новый массив чисел по возрастанию. Исходный массив не изменяется. */
export function sortNumbers(numbers) {
  return numbers.toSorted((a, b) => a - b);
}

/** Map "ключ -> количество" для элементов items; ключ вычисляет keyFn(item). Порядок - по первому появлению ключа. */
export function countBy(items, keyFn) {
  const counts = new Map();
  for (const item of items) {
    const key = keyFn(item);
    counts.set(key, (counts.get(key) ?? 0) + 1);
  }
  return counts;
}

/**
 * Ленивый генератор: из любого итерируемого объекта выдаёт массивы по size элементов
 * (последний может быть короче). Не читает источник дальше, чем нужно для очередной порции.
 */
export function* chunks(iterable, size) {
  let chunk = [];
  for (const item of iterable) {
    chunk.push(item);
    if (chunk.length === size) {
      yield chunk;
      chunk = [];
    }
  }
  if (chunk.length > 0) yield chunk;
}

/** Строка "<имя>:<возраст>:<число тегов>". Умолчания: имя "аноним", возраст 18, теги []. Работает и без аргумента. */
export function summarize({ name = "аноним", age = 18, tags = [] } = {}) {
  return `${name}:${age}:${tags.length}`;
}
