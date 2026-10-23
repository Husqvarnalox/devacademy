const total: number = 1200;
const age: number = 17;
const isMember: boolean = false;

// TODO: вместо false запишите условия, используя переменные выше.
// Бесплатная доставка: сумма заказа не меньше 1000
const freeDelivery: boolean = false;
// Детский билет: возраст меньше 18
const childTicket: boolean = false;
// Скидка: клиент состоит в клубе ИЛИ сумма заказа больше 5000
const discount: boolean = true;
// В бюджете: сумма не меньше 500 И не больше 1500
const inBudget: boolean = false;
// Не участник клуба: отрицание isMember
const notMember: boolean = false;

console.log("Бесплатная доставка:", freeDelivery);
console.log("Детский билет:", childTicket);
console.log("Скидка:", discount);
console.log("В бюджете:", inBudget);
console.log("Не участник клуба:", notMember);
