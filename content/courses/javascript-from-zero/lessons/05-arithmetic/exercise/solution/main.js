// Тысяча рублей делится поровну между тремя людьми.
const total = 1000;
const people = 3;
const each = Math.floor(total / people);
const rest = total % people;
const area = 7 ** 2;
console.log("Каждому:", each);
console.log("Остаток:", rest);
console.log("Площадь:", area);
