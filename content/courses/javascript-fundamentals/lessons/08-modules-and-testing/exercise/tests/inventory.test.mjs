import { describe, it, beforeEach } from "node:test";
import assert from "node:assert/strict";
import { Inventory } from "../src/inventory.mjs";

describe("Inventory", () => {
  let inv;
  beforeEach(() => {
    inv = new Inventory();
  });

  it("add накапливает остаток", () => {
    inv.add("abc-0001", 5);
    inv.add(" ABC-0001 ", 3);
    assert.equal(inv.stock("ABC-0001"), 8);
    assert.equal(inv.total, 8);
  });

  it("add возвращает this", () => {
    assert.equal(inv.add("ABC-0001", 1), inv);
    inv.add("ABC-0002", 2).add("ABC-0003", 3);
    assert.equal(inv.total, 6);
  });

  it("неизвестный артикул - остаток 0", () => {
    assert.equal(inv.stock("ABC-9999"), 0);
  });

  it("remove списывает и удаляет нулевые позиции", () => {
    inv.add("ABC-0001", 5);
    inv.remove("ABC-0001", 2);
    assert.equal(inv.stock("ABC-0001"), 3);
    inv.remove("ABC-0001", 3);
    assert.deepEqual(inv.skus(), []);
    assert.equal(inv.total, 0);
  });

  it("remove не уходит в минус", () => {
    inv.add("ABC-0001", 2);
    assert.throws(() => inv.remove("ABC-0001", 3), RangeError);
    assert.throws(() => inv.remove("ABC-0002", 1), RangeError);
    assert.equal(inv.stock("ABC-0001"), 2);
  });

  it("количество - положительное целое", () => {
    for (const bad of [0, -1, 1.5, "2", undefined, NaN]) {
      assert.throws(() => inv.add("ABC-0001", bad), RangeError, `add: ${String(bad)}`);
      assert.throws(() => inv.remove("ABC-0001", bad), RangeError, `remove: ${String(bad)}`);
    }
    assert.equal(inv.total, 0);
  });

  it("skus отсортирован", () => {
    inv.add("CCC-0001", 1).add("AAA-0002", 1).add("BBB-0003", 1);
    assert.deepEqual(inv.skus(), ["AAA-0002", "BBB-0003", "CCC-0001"]);
  });

  it("плохой артикул отвергается", () => {
    assert.throws(() => inv.add("oops", 1), RangeError);
    assert.throws(() => inv.stock(7), TypeError);
  });

  it("состояние склада не общее между экземплярами", () => {
    const other = new Inventory();
    inv.add("ABC-0001", 1);
    assert.equal(other.stock("ABC-0001"), 0);
  });
});
