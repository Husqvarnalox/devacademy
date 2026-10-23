export function isLeapYear(year: number): boolean {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

export function daysInMonth(year: number, month: number): number {
  if (month < 1 || month > 12) {
    return 0;
  }
  if (month === 2) {
    return isLeapYear(year) ? 29 : 28;
  }
  if (month === 4 || month === 6 || month === 9 || month === 11) {
    return 30;
  }
  return 31;
}

export function gcd(a: number, b: number): number {
  let x: number = a;
  let y: number = b;
  while (y !== 0) {
    const rest: number = x % y;
    x = y;
    y = rest;
  }
  return x;
}

export function countMultiples(limit: number, divisor: number): number {
  let count: number = 0;
  for (let n = 1; n <= limit; n++) {
    if (n % divisor === 0) {
      count++;
    }
  }
  return count;
}
