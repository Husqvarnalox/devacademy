export function sumTo(n: number): number {
  let total: number = 0;
  let i: number = 1;
  while (i <= n) {
    total = total + i;
    i = i + 1;
  }
  return total;
}

export function countDigits(n: number): number {
  if (n === 0) {
    return 1;
  }
  let digits: number = 0;
  let rest: number = n;
  while (rest > 0) {
    rest = Math.floor(rest / 10);
    digits = digits + 1;
  }
  return digits;
}

export function daysToSave(goal: number, perDay: number): number {
  let saved: number = 0;
  let days: number = 0;
  while (saved < goal) {
    saved = saved + perDay;
    days = days + 1;
  }
  return days;
}
