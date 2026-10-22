export function squares(n: number): number[] {
  const result: number[] = [];
  for (let i: number = 1; i <= n; i++) {
    result.push(i * i);
  }
  return result;
}

export function lastValue(values: number[]): number {
  if (values.length === 0) {
    return 0;
  }
  return values[values.length - 1];
}

export function sumOf(values: number[]): number {
  let total: number = 0;
  for (let i: number = 0; i < values.length; i++) {
    total = total + values[i];
  }
  return total;
}
