import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { formatPrice, parsePrice } from "../src/money.mjs";

describe("parsePrice", () => {
  it("целая цена", () => {
    assert.equal(parsePrice("7"), 700);
    assert.equal(parsePrice("0"), 0);
    assert.equal(parsePrice("120"), 12000);
  });

  it("дробная цена", () => {
    assert.equal(parsePrice("12.5"), 1250);
    assert.equal(parsePrice("0.05"), 5);
    assert.equal(parsePrice("19.99"), 1999);
  });

  it("пробелы по краям", () => {
    assert.equal(parsePrice("  3.20 "), 320);
  });

  it("отвергает неверный формат", () => {
    for (const bad of ["", "abc", "-1", "1.234", "1,5", ".5", "5.", "1e3", "1 2"]) {
      assert.throws(() => parsePrice(bad), RangeError, `для ${JSON.stringify(bad)}`);
    }
  });

  it("не строка", () => {
    assert.throws(() => parsePrice(12), TypeError);
    assert.throws(() => parsePrice(null), TypeError);
  });
});

describe("formatPrice", () => {
  it("копейки в строку", () => {
    assert.equal(formatPrice(1250), "12.50");
    assert.equal(formatPrice(5), "0.05");
    assert.equal(formatPrice(0), "0.00");
    assert.equal(formatPrice(100), "1.00");
  });

  it("отвергает неверные суммы", () => {
    for (const bad of [-1, 1.5, Number.NaN, Infinity, "100"]) {
      assert.throws(() => formatPrice(bad), RangeError, `для ${String(bad)}`);
    }
  });
});
