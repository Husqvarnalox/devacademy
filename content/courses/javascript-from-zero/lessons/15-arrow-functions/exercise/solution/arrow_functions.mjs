// Стрелочные функции и функции, которые принимают другие функции.
// Слово export делает значение видимым для проверки.

// Удвоенное число: double(4) -> 8.
// Запишите стрелкой: (n) => выражение
export const double = (n) => n * 2;

// true для чётного числа, иначе false: isEven(10) -> true, isEven(7) -> false.
export const isEven = (n) => n % 2 === 0;

/**
 * Вызывает fn дважды: сначала с x, затем с полученным результатом.
 * applyTwice(double, 3) -> 12.
 */
export function applyTwice(fn, x) {
  return fn(fn(x));
}

/**
 * Новый массив из результатов fn(item) для каждого элемента по порядку.
 * applyToEach([1, 2, 3], double) -> [2, 4, 6]. Для пустого массива - [].
 */
export function applyToEach(items, fn) {
  const result = [];
  for (const item of items) {
    result.push(fn(item));
  }
  return result;
}

/**
 * Сколько элементов, для которых test(item) вернула true.
 * countMatching([1, 2, 3, 4], isEven) -> 2.
 */
export function countMatching(items, test) {
  let count = 0;
  for (const item of items) {
    if (test(item)) {
      count++;
    }
  }
  return count;
}
