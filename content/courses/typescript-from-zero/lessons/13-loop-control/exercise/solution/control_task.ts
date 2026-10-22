export function firstDivisor(n: number): number {
  let found: number = n;
  for (let d: number = 2; d < n; d++) {
    if (n % d === 0) {
      found = d;
      break;
    }
  }
  return found;
}

export function sumSkipping(n: number, skip: number): number {
  let sum: number = 0;
  for (let i: number = 1; i <= n; i++) {
    if (i % skip === 0) {
      continue;
    }
    sum = sum + i;
  }
  return sum;
}

export function triangle(rows: number): string {
  let text: string = "";
  for (let r: number = 1; r <= rows; r++) {
    if (r > 1) {
      text = text + "\n";
    }
    for (let c: number = 1; c <= r; c++) {
      text = text + "*";
    }
  }
  return text;
}
