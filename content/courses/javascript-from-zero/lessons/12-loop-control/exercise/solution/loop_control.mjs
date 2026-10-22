// Три функции, в которых нужны break, continue и вложенные циклы.

/**
 * Простое ли число n: больше 1 и делится без остатка только на 1 и на себя.
 * isPrime(7) -> true, isPrime(9) -> false, isPrime(1) -> false.
 * Идея: найдите делитель среди чисел от 2 до n - 1; как только нашли, цикл можно остановить.
 */
export function isPrime(n) {
  if (n < 2) {
    return false;
  }
  let prime = true;
  for (let i = 2; i < n; i++) {
    if (n % i === 0) {
      prime = false;
      break;
    }
  }
  return prime;
}

/**
 * Сумма нечётных чисел от 1 до limit включительно: sumOfOdd(5) -> 1 + 3 + 5 = 9.
 * Если limit меньше 1, результат 0.
 * Идея: чётные числа пропускайте командой continue.
 */
export function sumOfOdd(limit) {
  let sum = 0;
  for (let i = 1; i <= limit; i++) {
    if (i % 2 === 0) {
      continue;
    }
    sum = sum + i;
  }
  return sum;
}

/**
 * Таблица умножения n на n в виде одной строки.
 * Числа в строке разделены одним пробелом, строки таблицы - символом "\n"
 * (после последней строки "\n" нет).
 * multiplicationTable(3) -> "1 2 3\n2 4 6\n3 6 9". Для n равного 0 результат - "".
 * Идея: внешний цикл по строкам, внутренний по столбцам; значение клетки - i * j.
 */
export function multiplicationTable(n) {
  let table = "";
  for (let i = 1; i <= n; i++) {
    if (i > 1) {
      table = table + "\n";
    }
    for (let j = 1; j <= n; j++) {
      if (j > 1) {
        table = table + " ";
      }
      table = table + i * j;
    }
  }
  return table;
}
