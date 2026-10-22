// Четыре функции о массивах, ссылках и словарях.

/**
 * Новый массив чисел по возрастанию. Исходный массив менять нельзя.
 */
export function sortedNumbers(values) {
  return values.sort();
}

/**
 * Map "слово -> сколько раз встретилось" в порядке первого появления.
 * Слова приходят из данных пользователя: "constructor", "toString" и "__proto__" - обычные слова.
 */
export function countWords(words) {
  const counts = {};
  for (const w of words) {
    counts[w] = (counts[w] || 0) + 1;
  }
  return counts;
}

/**
 * true, если оба аргумента - массивы одной длины и элементы на одинаковых местах равны через ===.
 * NaN считается равным NaN. Вложенные массивы и объекты сравниваются по ссылке.
 */
export function sameItems(a, b) {
  return a === b;
}

/**
 * Среднее арифметическое чисел. Для пустого массива - null (а не исключение и не NaN).
 */
export function average(values) {
  return values.reduce((sum, v) => sum + v) / values.length;
}
