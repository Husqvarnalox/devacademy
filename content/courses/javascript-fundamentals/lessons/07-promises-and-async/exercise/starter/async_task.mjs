// Промисы и async/await: параллельно, последовательно, с таймаутом и со сбором результатов.

const todo = (name) => Promise.reject(new Error(`${name} не реализована`));

/** Промис, который выполняется значением value через ms миллисекунд. */
export function sleep(ms, value) {
  return todo("sleep"); // TODO: new Promise + setTimeout
}

/** Запускает fn(item, index) для всех элементов сразу, возвращает массив результатов в исходном порядке. */
export function parallelMap(items, fn) {
  return todo("parallelMap"); // TODO: Promise.all
}

/**
 * Запускает fn(item, index) строго по очереди: следующий вызов начинается после завершения предыдущего.
 * При первой же ошибке возвращаемый промис отклоняется этой ошибкой, остальные элементы не запускаются.
 */
export async function sequentialMap(items, fn) {
  return todo("sequentialMap"); // TODO: цикл с await внутри
}

/**
 * Если promise не завершился за ms миллисекунд, результат отклоняется ошибкой Error("timeout").
 * Иначе результат повторяет исход promise (значение или причину отказа). Таймер должен быть очищен.
 */
export function withTimeout(promise, ms) {
  return todo("withTimeout"); // TODO: Promise.race
}

/**
 * Ждёт завершения всех промисов (не прерываясь на ошибках). Возвращает
 * { fulfilled: [значения успешных по порядку], rejected: [message причин отказа по порядку] }.
 */
export async function settleAll(promises) {
  return todo("settleAll"); // TODO: Promise.allSettled
}
