// Три функции с условиями. Слово export делает функцию видимой для проверки.

// Цена билета в рублях по возрасту:
// младше 7 лет - бесплатно (0), от 7 до 17 включительно - 150, от 18 до 64 - 300, от 65 лет - 100
export function ticketPrice(age: number): number {
  // TODO: проверьте возраст и верните цену
  return 0;
}

// Високосный ли год: делится на 4, но не на 100, либо делится на 400
export function isLeapYear(year: number): boolean {
  // TODO: верните true или false
  return false;
}

// Знак числа: "положительное", "отрицательное" или "ноль"
export function sign(n: number): string {
  // TODO: проверьте число и верните нужное слово
  return "";
}
