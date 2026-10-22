// Пять функций для работы со списками записей. Слово export делает функцию видимой для проверки.

/**
 * Запись из items, у которой свойство id равно id; если такой нет - undefined.
 * findById([{ id: 1 }, { id: 2 }], 2) -> { id: 2 }.
 */
export function findById(items, id) {
  return items.find((item) => item.id === id);
}

/**
 * Сумма price * qty по записям вида { name, price, qty }. Для пустого массива - 0.
 * totalPrice([{ name: "Чай", price: 10, qty: 3 }, { name: "Хлеб", price: 5, qty: 2 }]) -> 40.
 */
export function totalPrice(items) {
  let total = 0;
  for (const item of items) {
    total = total + item.price * item.qty;
  }
  return total;
}

/**
 * Запись с наименьшим price (при равных - первая). Для пустого массива - undefined.
 * cheapest([{ price: 30 }, { price: 10 }]) -> { price: 10 }.
 */
export function cheapest(items) {
  if (items.length === 0) {
    return undefined;
  }
  let best = items[0];
  for (const item of items) {
    if (item.price < best.price) {
      best = item;
    }
  }
  return best;
}

/**
 * Объект «клиент: сумма заказов» по записям вида { customer, amount }.
 * Ключи - в порядке первого появления клиента. Для пустого массива - {}.
 * groupTotals([{ customer: "Анна", amount: 100 }, { customer: "Борис", amount: 50 },
 *              { customer: "Анна", amount: 70 }]) -> { "Анна": 170, "Борис": 50 }.
 */
export function groupTotals(orders) {
  const totals = {};
  for (const order of orders) {
    if (order.customer in totals) {
      totals[order.customer] = totals[order.customer] + order.amount;
    } else {
      totals[order.customer] = order.amount;
    }
  }
  return totals;
}

/**
 * Сумма всех значений-чисел объекта. sumValues({ a: 1, b: 2 }) -> 3, sumValues({}) -> 0.
 */
export function sumValues(obj) {
  let sum = 0;
  for (const value of Object.values(obj)) {
    sum = sum + value;
  }
  return sum;
}
