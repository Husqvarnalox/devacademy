// Четыре функции о числах и условиях. Слово export делает функцию видимой для проверки.

// Високосный год: делится на 4, но не на 100, либо делится на 400.
export function isLeapYear(year: number): boolean {
  // TODO
  return false;
}

// Число дней в месяце (1 - январь, 12 - декабрь) для заданного года.
// Для месяца вне диапазона 1-12 верните 0. Февраль в високосный год - 29 дней.
export function daysInMonth(year: number, month: number): number {
  // TODO: используйте isLeapYear
  return 0;
}

// Наибольший общий делитель двух неотрицательных целых (алгоритм Евклида).
// gcd(12, 18) равно 6, gcd(7, 0) равно 7, gcd(0, 0) равно 0.
export function gcd(a: number, b: number): number {
  // TODO: пока b не равно 0, заменяйте пару (a, b) парой (b, a % b)
  return 0;
}

// Сколько чисел от 1 до limit включительно делятся на divisor без остатка.
// Если limit меньше 1, ответ 0.
export function countMultiples(limit: number, divisor: number): number {
  // TODO
  return 0;
}
