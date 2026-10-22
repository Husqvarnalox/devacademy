// Пять функций для работы со списками записей. Слово export делает функцию видимой для проверки.

/**
 * Запись из items, у которой свойство id равно id; если такой нет - undefined.
 * findById([{ id: 1 }, { id: 2 }], 2) -> { id: 2 }.
 */
export function findById(items, id) {
}

/**
 * Сумма price * qty по записям вида { name, price, qty }. Для пустого массива - 0.
 * totalPrice([{ name: "Чай", price: 10, qty: 3 }, { name: "Хлеб", price: 5, qty: 2 }]) -> 40.
 */
export function totalPrice(items) {
}

/**
 * Запись с наименьшим price (при равных - первая). Для пустого массива - undefined.
 * cheapest([{ price: 30 }, { price: 10 }]) -> { price: 10 }.
 */
export function cheapest(items) {
}

/**
 * Объект «клиент: сумма заказов» по записям вида { customer, amount }.
 * Ключи - в порядке первого появления клиента. Для пустого массива - {}.
 * groupTotals([{ customer: "Анна", amount: 100 }, { customer: "Борис", amount: 50 },
 *              { customer: "Анна", amount: 70 }]) -> { "Анна": 170, "Борис": 50 }.
 */
export function groupTotals(orders) {
}

/**
 * Сумма всех значений-чисел объекта. sumValues({ a: 1, b: 2 }) -> 3, sumValues({}) -> 0.
 */
export function sumValues(obj) {
}
