// Корзина покупателя. Цены не меняются, поэтому записаны в const; итог меняется, поэтому в let.
const breadPrice: number = 45;
const milkPrice: number = 80;
let total: number = 0;

// TODO: прибавьте к total цену хлеба, затем цену молока.
// Присваивание выглядит так: total = total + breadPrice;
console.log("Итого:", total);

// TODO: применена скидка 10: присвойте total новое значение, уменьшенное на 10.
console.log("К оплате:", total);
