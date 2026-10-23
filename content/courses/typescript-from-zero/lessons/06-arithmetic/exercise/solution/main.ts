const penPrice = 40;
const notebookPrice = 85;
const people = 4;

const total = 3 * penPrice + 2 * notebookPrice;
const share = Math.floor(total / people);
const rest = total % people;

console.log("Итого:", total);
console.log("На каждого:", share);
console.log("Остаток:", rest);
