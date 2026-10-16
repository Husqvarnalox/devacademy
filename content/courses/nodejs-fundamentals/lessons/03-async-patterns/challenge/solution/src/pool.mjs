/**
 * Применяет асинхронную fn к элементам, не запуская одновременно больше limit вызовов.
 * Результаты возвращаются в порядке элементов. Подробности - в условии урока.
 */
export async function mapLimit(items, limit, fn, { signal } = {}) {
  if (!Number.isInteger(limit) || limit < 1) {
    throw new RangeError("limit must be a positive integer");
  }
  if (signal?.aborted) throw signal.reason;

  const list = [...items];
  const results = new Array(list.length);
  const controller = new AbortController();

  return new Promise((resolve, reject) => {
    let next = 0;
    let running = 0;
    let done = 0;
    let finished = false;

    const fail = (error) => {
      if (finished) return;
      finished = true;
      controller.abort(error);
      signal?.removeEventListener("abort", onAbort);
      reject(error);
    };
    const onAbort = () => fail(signal.reason);

    const launch = () => {
      while (!finished && running < limit && next < list.length) {
        const index = next++;
        running++;
        let promise;
        try {
          promise = Promise.resolve(fn(list[index], index, controller.signal));
        } catch (error) {
          promise = Promise.reject(error);
        }
        promise.then(
          (value) => {
            running--;
            if (finished) return;
            results[index] = value;
            done++;
            if (done === list.length) {
              finished = true;
              signal?.removeEventListener("abort", onAbort);
              resolve(results);
            } else {
              launch();
            }
          },
          (error) => {
            running--;
            fail(error);
          },
        );
      }
    };

    signal?.addEventListener("abort", onAbort, { once: true });
    if (list.length === 0) {
      finished = true;
      signal?.removeEventListener("abort", onAbort);
      resolve(results);
    } else {
      launch();
    }
  });
}
