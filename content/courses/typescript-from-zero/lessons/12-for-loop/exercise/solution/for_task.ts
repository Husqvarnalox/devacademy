export function factorial(n: number): number {
  let result: number = 1;
  for (let i: number = 2; i <= n; i++) {
    result = result * i;
  }
  return result;
}

export function repeatText(text: string, times: number): string {
  let out: string = "";
  for (let i: number = 0; i < times; i++) {
    out = out + text;
  }
  return out;
}

export function countMultiples(limit: number, divisor: number): number {
  let count: number = 0;
  for (let i: number = 1; i <= limit; i++) {
    if (i % divisor === 0) {
      count = count + 1;
    }
  }
  return count;
}
