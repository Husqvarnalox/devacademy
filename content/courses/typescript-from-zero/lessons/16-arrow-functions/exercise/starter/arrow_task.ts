// Стрелочные функции и функции с обратным вызовом. Слово export делает значение видимым для проверки.

// Стрелочная функция: удваивает число. double(4) = 8
export const double = (n: number): number => {
  // TODO: верните удвоенное число (или замените тело на короткое выражение)
  return 0;
};

// Стрелочная функция: true, если число чётное. isEven(4) = true, isEven(7) = false
export const isEven = (n: number): boolean => {
  // TODO: число чётное, если остаток от деления на 2 равен 0
  return false;
};

// Новый массив, в котором к каждому элементу values применена функция f.
// Исходный массив менять нельзя. applyAll([1, 2, 3], double) = [2, 4, 6]
export function applyAll(values: number[], f: (n: number) => number): number[] {
  // TODO: создайте пустой массив и добавляйте в него f(v) для каждого элемента
  return [];
}

// Сколько элементов массива удовлетворяют проверке test (функции, возвращающей true или false).
// countWhere([1, 2, 3, 4], isEven) = 2
export function countWhere(values: number[], test: (n: number) => boolean): number {
  // TODO: для каждого элемента вызывайте test(v) и считайте результаты true
  return -1;
}
