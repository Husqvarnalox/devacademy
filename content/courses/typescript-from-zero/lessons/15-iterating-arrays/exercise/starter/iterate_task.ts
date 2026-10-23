// Три функции, которые обходят массив. Слово export делает функцию видимой для проверки.

// Наибольший элемент массива. Для пустого массива результат 0.
// Например, maxOf([3, 9, 4]) = 9, maxOf([-5, -2, -9]) = -2
export function maxOf(values: number[]): number {
  // TODO: начните с первого элемента и сравнивайте с ним остальные
  return -1;
}

// Сколько элементов массива строго больше limit.
// Например, countAbove([5, 8, 3, 8], 5) = 2
export function countAbove(values: number[], limit: number): number {
  // TODO: пройдите по массиву через for...of и считайте подходящие элементы
  return -1;
}

// Индекс первого элемента, равного target. Если такого элемента нет, результат -1.
// Например, indexOfValue([7, 3, 9, 3], 3) = 1
export function indexOfValue(values: number[], target: number): number {
  // TODO: индексный цикл; нашли - сразу return i
  return -2;
}
