const total: number = 1200;
const age: number = 17;
const isMember: boolean = false;

const freeDelivery: boolean = total >= 1000;
const childTicket: boolean = age < 18;
const discount: boolean = isMember || total > 5000;
const inBudget: boolean = total >= 500 && total <= 1500;
const notMember: boolean = !isMember;

console.log("Бесплатная доставка:", freeDelivery);
console.log("Детский билет:", childTicket);
console.log("Скидка:", discount);
console.log("В бюджете:", inBudget);
console.log("Не участник клуба:", notMember);
