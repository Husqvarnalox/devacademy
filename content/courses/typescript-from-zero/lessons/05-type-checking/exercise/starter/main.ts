// В программе четыре ошибки типов: значения не подходят к своим типам.
// Исправьте значения (типы после двоеточия менять не нужно), чтобы программа напечатала:
// Чай 3 true 15
const itemName: string = 120;
const quantity: number = "3";
const paid: boolean = "да";
let discount = 10;
discount = "15";
console.log(itemName, quantity, paid, discount);
