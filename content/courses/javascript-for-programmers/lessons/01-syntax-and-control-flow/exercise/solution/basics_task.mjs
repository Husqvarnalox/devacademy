// Четыре небольшие функции на ловушки JavaScript: дроби, "пустые" значения, NaN, строки вместо чисел.

export function addMoney(a, b) {
  if (!Number.isFinite(a) || !Number.isFinite(b)) {
    throw new TypeError("addMoney принимает только конечные числа");
  }
  return (Math.round(a * 100) + Math.round(b * 100)) / 100;
}

export function isBlank(value) {
  if (value === null || value === undefined) return true;
  return typeof value === "string" && value.trim() === "";
}

export function firstDefined(...values) {
  for (const v of values) {
    if (v !== null && v !== undefined) return v;
  }
  return undefined;
}

export function sumNumbers(values) {
  let total = 0;
  for (const v of values) {
    if (Number.isFinite(v)) total += v;
  }
  return total;
}
