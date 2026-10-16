import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { Inventory } from "../src/inventory.mjs";
import { formatReport, lowStock } from "../src/report.mjs";

function sample() {
  return new Inventory().add("BBB-0002", 10).add("AAA-0001", 2).add("CCC-0003", 5);
}

describe("formatReport", () => {
  it("строки по артикулам и итог", () => {
    assert.deepEqual(formatReport(sample()), ["AAA-0001: 2", "BBB-0002: 10", "CCC-0003: 5", "Всего: 3 поз., 17 ед."]);
  });

  it("пустой склад", () => {
    assert.deepEqual(formatReport(new Inventory()), ["Всего: 0 поз., 0 ед."]);
  });

  it("не Inventory - TypeError", () => {
    assert.throws(() => formatReport({ skus: () => [] }), TypeError);
    assert.throws(() => formatReport(null), TypeError);
  });
});

describe("lowStock", () => {
  it("остаток строго меньше порога", () => {
    assert.deepEqual(lowStock(sample(), 5), ["AAA-0001"]);
    assert.deepEqual(lowStock(sample(), 6), ["AAA-0001", "CCC-0003"]);
    assert.deepEqual(lowStock(sample(), 100), ["AAA-0001", "BBB-0002", "CCC-0003"]);
  });

  it("порог 0 - никого", () => {
    assert.deepEqual(lowStock(sample(), 0), []);
  });

  it("не Inventory - TypeError", () => {
    assert.throws(() => lowStock([], 1), TypeError);
  });
});
