// Пять функций на методах массивов (map, filter, find, includes, join, toSorted).
// Слово export делает функцию видимой для проверки. Исходный массив менять нельзя.

/**
 * Квадраты только чётных чисел, в прежнем порядке.
 * squaresOfEven([1, 2, 3, 4]) -> [4, 16].
 * Идея: сначала filter, потом map.
 */
export function squaresOfEven(numbers) {
  return numbers.filter((n) => n % 2 === 0).map((n) => n * n);
}

/**
 * Первое число, строго большее limit; если такого нет - undefined.
 * firstOver([3, 8, 12], 5) -> 8.
 */
export function firstOver(numbers, limit) {
  return numbers.find((n) => n > limit);
}

/**
 * Слова из words, которые есть в массиве known (порядок как в words).
 * knownWords(["кот", "пёс", "кит"], ["кот", "кит"]) -> ["кот", "кит"].
 */
export function knownWords(words, known) {
  return words.filter((w) => known.includes(w));
}

/**
 * Слова одной строкой через запятую и пробел. Для пустого массива - "".
 * commaList(["хлеб", "молоко"]) -> "хлеб, молоко".
 */
export function commaList(words) {
  return words.join(", ");
}

/**
 * НОВЫЙ массив чисел по возрастанию; исходный массив не меняется.
 * sortedAscending([10, 9, 100, 1]) -> [1, 9, 10, 100].
 * Идея: toSorted с компаратором (a, b) => ...
 */
export function sortedAscending(numbers) {
  return numbers.toSorted((a, b) => a - b);
}
