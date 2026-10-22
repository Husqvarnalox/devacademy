function check(numbers) {
  if (!Array.isArray(numbers) || !numbers.every((x) => Number.isFinite(x))) {
    throw new TypeError("expected an array of finite numbers");
  }
}

export function mean(numbers) {
  check(numbers);
  if (numbers.length === 0) return null;
  return numbers.reduce((sum, x) => sum + x, 0) / numbers.length;
}

export function median(numbers) {
  check(numbers);
  if (numbers.length === 0) return null;
  const sorted = [...numbers].sort((a, b) => a - b);
  const middle = sorted.length >> 1;
  return sorted.length % 2 === 1 ? sorted[middle] : (sorted[middle - 1] + sorted[middle]) / 2;
}

export function summarize(numbers) {
  check(numbers);
  if (numbers.length === 0) return { count: 0, mean: null, median: null, min: null, max: null };
  return {
    count: numbers.length,
    mean: mean(numbers),
    median: median(numbers),
    min: Math.min(...numbers),
    max: Math.max(...numbers),
  };
}
