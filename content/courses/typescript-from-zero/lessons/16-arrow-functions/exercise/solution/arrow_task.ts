export const double = (n: number): number => n * 2;

export const isEven = (n: number): boolean => n % 2 === 0;

export function applyAll(values: number[], f: (n: number) => number): number[] {
  const result: number[] = [];
  for (const v of values) {
    result.push(f(v));
  }
  return result;
}

export function countWhere(values: number[], test: (n: number) => boolean): number {
  let count: number = 0;
  for (const v of values) {
    if (test(v)) {
      count = count + 1;
    }
  }
  return count;
}
