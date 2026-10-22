// Три функции, в которых удобен цикл for.

/**
 * Факториал: произведение всех целых чисел от 1 до n.
 * factorial(5) -> 1 * 2 * 3 * 4 * 5 = 120. Факториал нуля равен 1.
 * Считайте, что n - целое число от 0 до 12.
 */
export function factorial(n) {
  let result = 1;
  for (let i = 2; i <= n; i++) {
    result = result * i;
  }
  return result;
}

/**
 * Повторяет текст times раз: repeatText("ab", 3) -> "ababab".
 * При times равном 0 результат - пустая строка "".
 */
export function repeatText(text, times) {
  let result = "";
  for (let i = 0; i < times; i++) {
    result = result + text;
  }
  return result;
}

/**
 * Сколько чисел от 1 до limit (включительно) делятся на k без остатка.
 * countMultiples(10, 3) -> 3 (это 3, 6 и 9).
 * Если limit меньше 1, результат 0. Считайте, что k - целое число больше 0.
 */
export function countMultiples(limit, k) {
  let count = 0;
  for (let i = 1; i <= limit; i++) {
    if (i % k === 0) {
      count++;
    }
  }
  return count;
}
