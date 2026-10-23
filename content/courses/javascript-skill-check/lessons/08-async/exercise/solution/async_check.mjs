export async function promisePool(tasks, limit) {
  if (!Number.isInteger(limit) || limit < 1) {
    throw new RangeError("limit must be an integer >= 1");
  }
  const results = new Array(tasks.length);
  let next = 0;
  let failed = false;

  async function worker() {
    while (!failed && next < tasks.length) {
      const index = next++;
      try {
        results[index] = await tasks[index]();
      } catch (error) {
        failed = true;
        throw error;
      }
    }
  }

  const workers = Array.from({ length: Math.min(limit, tasks.length) }, worker);
  await Promise.all(workers);
  return results;
}

export async function withTimeout(task, ms) {
  const controller = new AbortController();
  let timerId;
  const timeout = new Promise((_, reject) => {
    timerId = setTimeout(() => {
      controller.abort();
      const error = new Error(`timed out after ${ms} ms`);
      error.name = "TimeoutError";
      reject(error);
    }, ms);
  });
  try {
    return await Promise.race([Promise.resolve().then(() => task(controller.signal)), timeout]);
  } finally {
    clearTimeout(timerId);
  }
}
