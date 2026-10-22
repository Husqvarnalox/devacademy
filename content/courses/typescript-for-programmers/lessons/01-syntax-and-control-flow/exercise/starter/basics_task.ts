// Четыре функции о числах и условиях. Слово export делает функцию видимой для проверки.

// Округляет сумму до копеек (двух знаков после запятой): умножьте на 100,
// округлите через Math.round и поделите на 100. roundMoney(0.1 + 0.2) должно дать 0.3.
export function roundMoney(amount: number): number {
  // TODO
  return amount;
}

// Вид числа: "не число" для NaN, "бесконечность" для Infinity и -Infinity,
// "целое" для целых чисел и "дробное" для остальных.
// Подсказка: Number.isNaN, Number.isFinite, Number.isInteger.
export function classifyNumber(x: number): string {
  // TODO
  return "целое";
}

// Сумма цифр целого числа; знак игнорируется. digitSum(-907) равно 16, digitSum(0) равно 0.
export function digitSum(n: number): number {
  // TODO: цикл while, остаток от деления на 10 и Math.floor(rest / 10)
  return 0;
}

// Цена билета в рублях.
//   младше 6 лет - 0; от 6 до 17 включительно - 300;
//   65 лет и старше - 250 (раньше студенческой скидки);
//   студент (остальные возрасты) - 350; все прочие - 500.
export function ticketPrice(age: number, isStudent: boolean): number {
  // TODO: порядок проверок важен
  return 500;
}
