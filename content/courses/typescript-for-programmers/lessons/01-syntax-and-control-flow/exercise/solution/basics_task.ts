export function roundMoney(amount: number): number {
  return Math.round(amount * 100) / 100;
}

export function classifyNumber(x: number): string {
  if (Number.isNaN(x)) {
    return "не число";
  }
  if (!Number.isFinite(x)) {
    return "бесконечность";
  }
  if (Number.isInteger(x)) {
    return "целое";
  }
  return "дробное";
}

export function digitSum(n: number): number {
  let rest: number = Math.abs(n);
  let sum: number = 0;
  while (rest > 0) {
    sum += rest % 10;
    rest = Math.floor(rest / 10);
  }
  return sum;
}

export function ticketPrice(age: number, isStudent: boolean): number {
  if (age < 6) {
    return 0;
  }
  if (age <= 17) {
    return 300;
  }
  if (age >= 65) {
    return 250;
  }
  if (isStudent) {
    return 350;
  }
  return 500;
}
