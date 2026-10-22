// Харнесс DevAcademy: импортирует модуль ученика и печатает по одной строке на тест.
const NONCE = process.env.DEVACADEMY_NONCE;
const report = (id, passed, message = "") =>
  console.log("##DEVACADEMY##", NONCE, JSON.stringify({ id, passed: Boolean(passed), message }));

let m = null;
let loadError = null;
try {
  m = await import("./orders_check.mjs");
} catch (e) {
  loadError = e;
}

async function test(id, check) {
  if (loadError) return report(id, false, `модуль не загрузился: ${loadError?.name}: ${loadError?.message}`);
  try {
    const problem = await check();
    report(id, problem == null, problem ?? "");
  } catch (e) {
    report(id, false, `исключение ${e?.name}: ${e?.message}`);
  }
}

const json = (v) => JSON.stringify(v);
function expectEach(fn, name, cases) {
  for (const [args, want] of cases) {
    const got = fn(...args);
    if (json(got) !== json(want) || !Object.is(typeof got, typeof want)) return `${name}(${args.map(json).join(", ")}) вернуло ${json(got)}, ожидалось ${json(want)}`;
  }
  return null;
}
const wantParse = (what, fn, field) => {
  let e;
  try {
    fn();
  } catch (err) {
    e = err;
  }
  if (!e) return `${what} должно бросать ParseError`;
  if (!(e instanceof m.ParseError)) return `${what} бросило ${e?.name}, ожидалось ParseError`;
  return e.field === field ? null : `${what}: field = ${json(e.field)}, ожидалось ${json(field)}`;
};

await test("line-basic", () =>
  expectEach(m.parseOrderLine, "parseOrderLine", [
    [["AB-123,2,9.99"], { sku: "AB-123", qty: 2, priceCents: 999 }],
    [[" CD-001 , 10 , 5 "], { sku: "CD-001", qty: 10, priceCents: 500 }],
    [["EF-777,1,0.5"], { sku: "EF-777", qty: 1, priceCents: 50 }],
  ]));
await test("orders-basic", () => {
  const got = m.parseOrders("AB-123,2,9.99\nxx,1,1\nCD-001,1,0.01");
  const want = { orders: [{ sku: "AB-123", qty: 2, priceCents: 999 }, { sku: "CD-001", qty: 1, priceCents: 1 }], errors: [{ line: 2, field: "sku" }], totalCents: 1999 };
  return json(got) === json(want) ? null : `parseOrders вернуло ${json(got)}, ожидалось ${json(want)}`;
});

await test("line-errors", () =>
  wantParse('parseOrderLine("AB-123,2")', () => m.parseOrderLine("AB-123,2"), "fields") ??
  wantParse('parseOrderLine("")', () => m.parseOrderLine(""), "fields") ??
  wantParse('parseOrderLine("AB-123,2,1,1")', () => m.parseOrderLine("AB-123,2,1,1"), "fields") ??
  wantParse('parseOrderLine("ab-123,2,1")', () => m.parseOrderLine("ab-123,2,1"), "sku") ??
  wantParse('parseOrderLine("AB-12,2,1")', () => m.parseOrderLine("AB-12,2,1"), "sku") ??
  wantParse('parseOrderLine("AB-123,0,1")', () => m.parseOrderLine("AB-123,0,1"), "qty") ??
  wantParse('parseOrderLine("AB-123,,1")', () => m.parseOrderLine("AB-123,,1"), "qty") ??
  wantParse('parseOrderLine("AB-123,1.5,1")', () => m.parseOrderLine("AB-123,1.5,1"), "qty") ??
  wantParse('parseOrderLine("AB-123,2x,1")', () => m.parseOrderLine("AB-123,2x,1"), "qty") ??
  wantParse('parseOrderLine("AB-123,2,")', () => m.parseOrderLine("AB-123,2,"), "price") ??
  wantParse('parseOrderLine("AB-123,2,1.234")', () => m.parseOrderLine("AB-123,2,1.234"), "price") ??
  wantParse('parseOrderLine("AB-123,2,-1")', () => m.parseOrderLine("AB-123,2,-1"), "price") ??
  wantParse('parseOrderLine("AB-123,2,1e3")', () => m.parseOrderLine("AB-123,2,1e3"), "price") ??
  wantParse('parseOrderLine("x,0,y")', () => m.parseOrderLine("x,0,y"), "sku"));
await test("orders-edge", () => {
  const got = m.parseOrders("\r\nAB-123,1,1\r\n  \nAB-1,1,1\nCD-001,x,1\nEF-777,3,2.5\n");
  const want = { orders: [{ sku: "AB-123", qty: 1, priceCents: 100 }, { sku: "EF-777", qty: 3, priceCents: 250 }], errors: [{ line: 4, field: "sku" }, { line: 5, field: "qty" }], totalCents: 850 };
  if (json(got) !== json(want)) return `parseOrders вернуло ${json(got)}, ожидалось ${json(want)}`;
  if (json(m.parseOrders("")) !== json({ orders: [], errors: [], totalCents: 0 })) return "для пустого текста ожидались пустые списки и 0";
  for (const bad of [null, undefined, 5, ["AB-123,1,1"]]) {
    try {
      m.parseOrders(bad);
      return `parseOrders(${json(bad)}) должно бросать TypeError`;
    } catch (e) {
      if (!(e instanceof TypeError)) return `parseOrders(${json(bad)}) бросило ${e?.name}, ожидалось TypeError`;
    }
  }
  return null;
});
await test("total-edge", () => {
  const got = m.parseOrders("AB-123,3,0.10\nAB-124,1,0.20\nAB-125,7,19.99");
  if (got.totalCents !== 3 * 10 + 20 + 7 * 1999) return `totalCents = ${got.totalCents}, ожидалось ${3 * 10 + 20 + 7 * 1999}`;
  return Number.isInteger(got.orders[2].priceCents) && got.orders[2].priceCents === 1999 ? null : "цена 19.99 должна стать целым числом 1999";
});
