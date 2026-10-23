export function ticketPrice(age: number): number {
  if (age < 7) {
    return 0;
  } else if (age < 18) {
    return 150;
  } else if (age < 65) {
    return 300;
  } else {
    return 100;
  }
}

export function isLeapYear(year: number): boolean {
  if (year % 400 === 0) {
    return true;
  }
  if (year % 100 === 0) {
    return false;
  }
  return year % 4 === 0;
}

export function sign(n: number): string {
  if (n > 0) {
    return "положительное";
  } else if (n < 0) {
    return "отрицательное";
  } else {
    return "ноль";
  }
}
