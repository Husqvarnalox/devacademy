import { describe, it } from "node:test";
import assert from "node:assert/strict";
import { isValidSku, normalizeSku } from "../src/sku.mjs";

describe("isValidSku", () => {
  it("принимает верный формат", () => {
    assert.equal(isValidSku("ABC-0001"), true);
    assert.equal(isValidSku("ZZZ-9999"), true);
  });

  it("отвергает неверный формат", () => {
    for (const bad of ["abc-0001", "AB-0001", "ABCD-0001", "ABC-001", "ABC0001", " ABC-0001", "ABC-0001 ", "", "ABC-00a1"]) {
      assert.equal(isValidSku(bad), false, `для ${JSON.stringify(bad)}`);
    }
  });

  it("не строка - false", () => {
    assert.equal(isValidSku(null), false);
    assert.equal(isValidSku(1234), false);
    assert.equal(isValidSku(undefined), false);
  });
});

describe("normalizeSku", () => {
  it("обрезает пробелы и переводит в верхний регистр", () => {
    assert.equal(normalizeSku("  abc-0042 "), "ABC-0042");
    assert.equal(normalizeSku("ABC-0042"), "ABC-0042");
  });

  it("неверный формат - RangeError", () => {
    for (const bad of ["", "abc", "abc-12", "ab-0001", "abc-00001"]) {
      assert.throws(() => normalizeSku(bad), RangeError, `для ${JSON.stringify(bad)}`);
    }
  });

  it("не строка - TypeError", () => {
    assert.throws(() => normalizeSku(42), TypeError);
    assert.throws(() => normalizeSku(null), TypeError);
  });
});
