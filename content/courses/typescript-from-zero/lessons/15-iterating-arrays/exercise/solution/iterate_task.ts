export function maxOf(values: number[]): number {
  if (values.length === 0) {
    return 0;
  }
  let best: number = values[0];
  for (const v of values) {
    if (v > best) {
      best = v;
    }
  }
  return best;
}

export function countAbove(values: number[], limit: number): number {
  let count: number = 0;
  for (const v of values) {
    if (v > limit) {
      count = count + 1;
    }
  }
  return count;
}

export function indexOfValue(values: number[], target: number): number {
  for (let i: number = 0; i < values.length; i++) {
    if (values[i] === target) {
      return i;
    }
  }
  return -1;
}
