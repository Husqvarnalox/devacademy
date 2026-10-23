const customer = "Анна Петрова";
const item = "Чай";
const quantity = 3;
const price = 120;

const line1 = `Покупатель: ${customer}`;
const line2 = `${item} x ${quantity} = ${quantity * price}`;
const line3 = "Букв в имени: " + customer.length;

console.log(line1);
console.log(line2);
console.log(line3);
