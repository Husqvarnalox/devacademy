// Модуль статистики: точный набор именованных экспортов и чистые функции.

// TODO: этот экспорт лишний - у модуля должны быть только mean, median и summarize
export const DEBUG = true;

/** Среднее арифметическое; пустой массив даёт null. */
export function mean(numbers) {
  // TODO: проверьте вход (TypeError) и пустой массив
  return numbers.reduce((sum, x) => sum + x, 0) / numbers.length;
}

/** Медиана; пустой массив даёт null. Исходный массив менять нельзя. */
export function median(numbers) {
  // TODO: sort меняет массив на месте и сортирует по строкам
  const sorted = numbers.sort();
  return sorted[Math.floor(sorted.length / 2)];
}

// TODO: добавьте экспортируемую функцию summarize(numbers) -> { count, mean, median, min, max }
