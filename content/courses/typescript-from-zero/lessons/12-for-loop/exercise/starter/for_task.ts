// Три функции с циклом for. Слово export делает функцию видимой для проверки.

// Произведение целых чисел от 1 до n. Для n меньше 1 результат 1.
// Например, factorial(4) = 1 * 2 * 3 * 4 = 24
export function factorial(n: number): number {
  // TODO: накопитель с начальным значением 1 и цикл for от 2 до n включительно
  return -1;
}

// Строка, в которой text повторён times раз подряд. Для times меньше 1 результат "" (пустая строка).
// Например, repeatText("ab", 3) = "ababab"
export function repeatText(text: string, times: number): string {
  // TODO: накопитель-строка и цикл for, который выполняется ровно times раз
  return "?";
}

// Сколько чисел от 1 до limit включительно делятся на divisor без остатка. Считайте, что divisor больше 0.
// Например, countMultiples(10, 3) = 3 (это 3, 6 и 9)
export function countMultiples(limit: number, divisor: number): number {
  // TODO: цикл for от 1 до limit; для каждого числа проверьте остаток от деления
  return -1;
}
