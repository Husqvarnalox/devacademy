import { describe, test } from "node:test";
import assert from "node:assert/strict";
import { mapLimit } from "../src/pool.mjs";

// Даёт всем промисам и микрозадачам завершиться: setImmediate срабатывает после очереди микрозадач.
const flush = () => new Promise((resolve) => setImmediate(resolve));

function deferred() {
  let resolve;
  let reject;
  const promise = new Promise((res, rej) => {
    resolve = res;
    reject = rej;
  });
  return { promise, resolve, reject };
}

describe("mapLimit", () => {
  test("результаты в порядке элементов", async () => {
    const gates = [deferred(), deferred(), deferred()];
    const pending = mapLimit([0, 1, 2], 3, (item) => gates[item].promise);
    gates[2].resolve("c");
    gates[0].resolve("a");
    gates[1].resolve("b");
    assert.deepEqual(await pending, ["a", "b", "c"]);
  });

  test("не больше limit вызовов одновременно", async () => {
    let running = 0;
    let max = 0;
    const gates = Array.from({ length: 6 }, deferred);
    const pending = mapLimit(gates, 2, async (gate) => {
      running++;
      max = Math.max(max, running);
      await gate.promise;
      running--;
    });
    for (const gate of gates) {
      await flush();
      gate.resolve();
    }
    await pending;
    assert.equal(max, 2);
  });

  test("следующий элемент стартует, как только освободился слот", async () => {
    const started = [];
    const gates = [deferred(), deferred(), deferred(), deferred()];
    const pending = mapLimit([0, 1, 2, 3], 2, (i) => {
      started.push(i);
      return gates[i].promise;
    });
    await flush();
    assert.deepEqual(started, [0, 1]);
    gates[1].resolve();
    await flush();
    assert.deepEqual(started, [0, 1, 2]);
    gates[0].resolve();
    await flush();
    assert.deepEqual(started, [0, 1, 2, 3]);
    gates[2].resolve();
    gates[3].resolve();
    await pending;
  });

  test("первая ошибка отклоняет результат и новые вызовы не стартуют", async () => {
    const started = [];
    const boom = new Error("boom");
    const pending = mapLimit([0, 1, 2], 1, async (i) => {
      started.push(i);
      if (i === 0) throw boom;
      return i;
    });
    await assert.rejects(pending, (error) => error === boom);
    await flush();
    assert.deepEqual(started, [0]);
  });

  test("ошибка прерывает signal, переданный в fn", async () => {
    const gate = deferred();
    const signals = [];
    const boom = new Error("boom");
    const pending = mapLimit([0, 1], 2, (i, index, signal) => {
      signals.push(signal);
      return i === 0 ? gate.promise : Promise.reject(boom);
    });
    await assert.rejects(pending, (error) => error === boom);
    assert.equal(signals.length, 2);
    assert.ok(signals.every((signal) => signal.aborted));
    assert.equal(signals[0].reason, boom);
    gate.resolve();
  });

  test("внешний signal: отмена во время работы", async () => {
    const controller = new AbortController();
    const started = [];
    const gate = deferred();
    const pending = mapLimit([0, 1, 2], 1, (i) => {
      started.push(i);
      return gate.promise;
    }, { signal: controller.signal });
    await flush();
    const reason = new Error("stop");
    controller.abort(reason);
    await assert.rejects(pending, (error) => error === reason);
    gate.resolve();
    await flush();
    assert.deepEqual(started, [0]);
  });

  test("уже отменённый signal: fn не вызывается", async () => {
    let calls = 0;
    const signal = AbortSignal.abort();
    await assert.rejects(
      mapLimit([1, 2], 2, async () => { calls++; }, { signal }),
      (error) => error === signal.reason && error.name === "AbortError",
    );
    assert.equal(calls, 0);
  });

  test("limit меньше 1 - RangeError", async () => {
    await assert.rejects(mapLimit([1], 0, async (x) => x), RangeError);
    await assert.rejects(mapLimit([1], 1.5, async (x) => x), RangeError);
  });

  test("пустой список и limit больше длины", async () => {
    assert.deepEqual(await mapLimit([], 3, async (x) => x), []);
    assert.deepEqual(await mapLimit([1, 2], 10, async (x) => x * 10), [10, 20]);
  });

  test("fn получает индекс и работает с синхронным значением", async () => {
    const seen = [];
    const out = await mapLimit(["a", "b"], 1, (item, index) => {
      seen.push([item, index]);
      return item.toUpperCase();
    });
    assert.deepEqual(out, ["A", "B"]);
    assert.deepEqual(seen, [["a", 0], ["b", 1]]);
  });

  test("синхронное исключение в fn отклоняет результат", async () => {
    const boom = new Error("sync boom");
    await assert.rejects(mapLimit([1], 1, () => { throw boom; }), (error) => error === boom);
  });
});
