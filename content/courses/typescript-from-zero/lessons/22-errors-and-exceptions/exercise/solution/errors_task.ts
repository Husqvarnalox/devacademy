export function safeDivide(a: number, b: number): number {
  if (b === 0) {
    throw new Error("деление на ноль");
  }
  return a / b;
}

export function checkAge(age: number): number {
  if (Math.floor(age) !== age) {
    throw new Error("возраст должен быть целым числом");
  }
  if (age < 0 || age > 150) {
    throw new Error("возраст вне диапазона 0-150");
  }
  return age;
}

export function divideText(a: number, b: number): string {
  try {
    return "результат: " + safeDivide(a, b);
  } catch (error) {
    if (error instanceof Error) {
      return "ошибка: " + error.message;
    }
    return "неизвестная ошибка";
  }
}

export function validAges(ages: number[]): number[] {
  const result: number[] = [];
  for (const age of ages) {
    try {
      result.push(checkAge(age));
    } catch (error) {
      // недопустимый возраст пропускаем
    }
  }
  return result;
}
