// Старая библиотека без типов (имитация пакета из npm без объявлений).
export const VERSION = "1.2.0";

export function clamp(n, lo, hi) {
  return Math.min(Math.max(n, lo), hi);
}

export function mean(xs) {
  if (xs.length === 0) return 0;
  return xs.reduce((a, b) => a + b, 0) / xs.length;
}
