// Три небольшие функции. Слово export делает функцию видимой для проверки.
// После каждого имени параметра стоит его тип, после скобок - тип результата.

// Возвращает прощание вида "До свидания, Анна!"
export function farewell(name: string): string {
  // TODO: верните строку "До свидания, ", затем name, затем "!"
  return "";
}

// Возвращает стоимость позиции чека: цена, умноженная на количество
export function lineTotal(price: number, quantity: number): number {
  // TODO: верните произведение цены и количества
  return 0;
}

// Возвращает цену после скидки в процентах: 200 со скидкой 10 процентов даёт 180
export function discounted(price: number, percent: number): number {
  // TODO: цена умножается на (100 - percent) и делится на 100
  return 0;
}
