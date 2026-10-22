// Три функции для работы с массивами. Слово export делает функцию видимой для проверки.

/**
 * Последний элемент массива. Для пустого массива - undefined.
 * lastItem([4, 5, 6]) -> 6, lastItem([]) -> undefined.
 * Идея: индекс последнего элемента на единицу меньше длины массива.
 */
export function lastItem(items) {
  return items[items.length - 1];
}

/**
 * Новый массив квадратов чисел от 1 до n. Для n меньше 1 - пустой массив.
 * squares(4) -> [1, 4, 9, 16].
 * Идея: пустой массив и цикл, в котором каждый квадрат добавляется командой push.
 */
export function squares(n) {
  const result = [];
  for (let i = 1; i <= n; i++) {
    result.push(i * i);
  }
  return result;
}

/**
 * Меняет местами первый и последний элементы САМОГО массива и возвращает его.
 * swapFirstAndLast([1, 2, 3]) -> [3, 2, 1]. Если элементов меньше двух - вернуть массив как есть.
 * Идея: одно из значений нужно сохранить во временную переменную.
 */
export function swapFirstAndLast(items) {
  if (items.length < 2) {
    return items;
  }
  const first = items[0];
  items[0] = items[items.length - 1];
  items[items.length - 1] = first;
  return items;
}
