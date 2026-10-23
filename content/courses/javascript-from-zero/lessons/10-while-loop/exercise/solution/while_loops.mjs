// Три функции, в которых нужен цикл while.

/**
 * Сумма всех целых чисел от 1 до n: sumTo(4) -> 1 + 2 + 3 + 4 = 10.
 * Если n меньше 1, результат 0.
 */
export function sumTo(n) {
  let sum = 0;
  let i = 1;
  while (i <= n) {
    sum = sum + i;
    i = i + 1;
  }
  return sum;
}

/**
 * Сколько цифр в неотрицательном целом числе: countDigits(2024) -> 4.
 * У числа 0 одна цифра.
 * Подсказка по идее: пока в числе есть хотя бы две цифры, отбрасывайте последнюю
 * (Math.floor(n / 10)) и считайте шаги.
 */
export function countDigits(n) {
  let digits = 1;
  let rest = n;
  while (rest >= 10) {
    rest = Math.floor(rest / 10);
    digits = digits + 1;
  }
  return digits;
}

/**
 * Сколько дней нужно копить, откладывая perDay рублей в день, чтобы набрать goal рублей.
 * daysToSave(100, 30) -> 4, потому что за 3 дня только 90.
 * Если goal не больше 0, дней нужно 0. Считайте, что perDay больше 0.
 */
export function daysToSave(goal, perDay) {
  let saved = 0;
  let days = 0;
  while (saved < goal) {
    saved = saved + perDay;
    days = days + 1;
  }
  return days;
}
