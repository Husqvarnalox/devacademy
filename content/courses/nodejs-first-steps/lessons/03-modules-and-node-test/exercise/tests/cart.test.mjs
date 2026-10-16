import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { cartTotal, lineTotal, receipt } from "../src/cart.mjs";

const tea = { name: "Чай", price: "3.50", qty: 2 };
const bread = { name: "Хлеб", price: "42", qty: 1 };

describe("lineTotal", () => {
  it("неверное количество", () => {
    for (const qty of [0, -2, 1.5, "2", undefined]) {
      assert.throws(() => lineTotal({ name: "X", price: "1", qty }), RangeError, `для qty=${String(qty)}`);
    }
  });
});

describe("cartTotal", () => {
  it("пустая корзина", () => {
    assert.equal(cartTotal([]), 0);
  });

  it("сумма по строкам", () => {
    assert.equal(cartTotal([tea, bread]), 4900);
    assert.equal(cartTotal([{ name: "Спички", price: "0.07", qty: 3 }]), 21);
  });
});

describe("receipt", () => {
  it("строки чека", () => {
    assert.deepEqual(receipt([tea, bread]), ["Чай x2 = 7.00", "Хлеб x1 = 42.00", "Итого: 49.00"]);
  });

  it("пустой чек", () => {
    assert.deepEqual(receipt([]), ["Итого: 0.00"]);
  });
});
