// Четыре функции написаны, но каждая работает неверно из-за ошибки с областью видимости.
// Найдите ошибки и исправьте их. Условия работы функций описаны в комментариях.

/**
 * Количество чётных чисел в массиве.
 */
export function countEven(numbers) {
  let count = 0;
  for (const n of numbers) {
    if (n % 2 === 0) {
      count++;
    }
  }
  return count;
}

/**
 * Наибольшее число непустого массива.
 */
export function maxOf(numbers) {
  let best = numbers[0];
  for (const n of numbers) {
    if (n > best) {
      best = n;
    }
  }
  return best;
}

/**
 * Оценка по баллам: от 90 - "отлично", от 70 - "хорошо", от 50 - "зачёт", иначе "незачёт".
 */
export function grade(score) {
  let label = "";
  if (score >= 90) {
    label = "отлично";
  } else if (score >= 70) {
    label = "хорошо";
  } else if (score >= 50) {
    label = "зачёт";
  } else {
    label = "незачёт";
  }
  return label;
}

/**
 * Номер первого элемента, равного target, или -1, если такого нет.
 */
export function indexOfValue(items, target) {
  let found = -1;
  for (let i = 0; i < items.length; i++) {
    if (items[i] === target) {
      found = i;
      break;
    }
  }
  return found;
}
