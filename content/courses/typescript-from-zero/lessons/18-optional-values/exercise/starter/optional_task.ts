// Необязательные значения. Слово export делает функцию видимой для проверки.

// Первое отрицательное число в массиве. Если отрицательных нет, результат undefined.
// Например, findFirstNegative([4, -2, 7, -9]) = -2; findFirstNegative([4, 0, 7]) = undefined
export function findFirstNegative(values: number[]): number | undefined {
  // TODO: пройдите по массиву; нашли отрицательное - верните его; после цикла верните undefined
  return -1;
}

// Подпись к цене: "нет цены", если price равно undefined, иначе "<цена> руб." (например, "250 руб.").
export function describePrice(price: number | undefined): string {
  // TODO: сначала отсейте undefined проверкой, потом используйте число
  return "?";
}

// Элемент массива с номером index (отрицательный индекс считается с конца: -1 - последний).
// Если такого элемента нет, результат fallback. Используйте at и ??.
// itemOrDefault([4, 5, 6], 1, 0) = 5; itemOrDefault([4, 5, 6], -1, 0) = 6; itemOrDefault([4, 5, 6], 9, 0) = 0
export function itemOrDefault(values: number[], index: number, fallback: number): number {
  // TODO: values.at(index) ?? fallback
  return -999;
}
