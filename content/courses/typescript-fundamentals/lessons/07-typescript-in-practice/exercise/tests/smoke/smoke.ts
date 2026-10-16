// Запускается самим Node (удаление типов) из теста runtime.test.ts: так проверяется, что в коде нет синтаксиса, требующего преобразования.
import { Status, isStatus, label, Counter } from "../src/status.ts";
import { nth, lastOf, head, withRetries, retriesOf } from "../src/list.ts";
import { clampAll, average, libVersion } from "../src/stats.ts";

console.log(
  JSON.stringify({
    statuses: Object.values(Status),
    is: [isStatus("active"), isStatus("x"), isStatus(null)],
    label: label(Status.Blocked),
    counter: new Counter().increment().increment().value,
    list: [nth([1, 2], 1), nth([1], 5) ?? null, lastOf([] as string[]) ?? null, head(["a", "b"])],
    retries: [retriesOf({}), retriesOf({ retries: 0 }), "retries" in withRetries({ retries: 2 }, undefined)],
    stats: [clampAll([-5, 5, 50], 0, 10), average([2, 4]), libVersion()],
  }),
);
