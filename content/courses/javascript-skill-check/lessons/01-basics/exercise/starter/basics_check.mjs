// Четыре небольшие функции. Для сравнения используйте ===.

/** "FizzBuzz" для кратных 15, "Fizz" для кратных 3, "Buzz" для кратных 5, иначе число строкой. */
export function fizzBuzz(n) {
  if (n % 3 === 0) return "Fizz";
  if (n % 5 === 0) return "Buzz";
  return n;
}

/** Сумма чисел от 1 до limit - 1, кратных 3 или 5. */
export function sumOfMultiples(limit) {
  let total = 0;
  for (let i = 1; i <= limit; i++) {
    if (i % 3 === 0 || i % 5 === 0) total += i;
  }
  return total;
}

/** Високосный год: кратен 4, но не 100, кроме кратных 400. */
export function isLeapYear(year) {
  return year % 4 === 0;
}

/** Наименьшее простое число, строго большее n. */
export function firstPrimeAbove(n) {
  return n + 1;
}
