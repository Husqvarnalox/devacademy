// Промисы и async/await: параллельно, последовательно, с таймаутом и со сбором результатов.

/** Промис, который выполняется значением value через ms миллисекунд. */
export function sleep(ms, value) {
  return new Promise((resolve) => setTimeout(resolve, ms, value));
}

/** Запускает fn(item, index) для всех элементов сразу, возвращает массив результатов в исходном порядке. */
export function parallelMap(items, fn) {
  return Promise.all(items.map((item, index) => fn(item, index)));
}

/**
 * Запускает fn(item, index) строго по очереди: следующий вызов начинается после завершения предыдущего.
 * При первой же ошибке возвращаемый промис отклоняется этой ошибкой, остальные элементы не запускаются.
 */
export async function sequentialMap(items, fn) {
  const results = [];
  for (let index = 0; index < items.length; index++) {
    results.push(await fn(items[index], index));
  }
  return results;
}

/**
 * Если promise не завершился за ms миллисекунд, результат отклоняется ошибкой Error("timeout").
 * Иначе результат повторяет исход promise (значение или причину отказа). Таймер должен быть очищен.
 */
export function withTimeout(promise, ms) {
  let timer;
  const timeout = new Promise((_, reject) => {
    timer = setTimeout(() => reject(new Error("timeout")), ms);
  });
  return Promise.race([promise, timeout]).finally(() => clearTimeout(timer));
}

/**
 * Ждёт завершения всех промисов (не прерываясь на ошибках). Возвращает
 * { fulfilled: [значения успешных по порядку], rejected: [message причин отказа по порядку] }.
 */
export async function settleAll(promises) {
  const results = await Promise.allSettled(promises);
  const fulfilled = [];
  const rejected = [];
  for (const r of results) {
    if (r.status === "fulfilled") fulfilled.push(r.value);
    else rejected.push(r.reason?.message ?? String(r.reason));
  }
  return { fulfilled, rejected };
}
