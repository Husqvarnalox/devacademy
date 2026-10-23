// Асинхронность: пул промисов с лимитом и тайм-аут с AbortController.

/**
 * Запускает задачи (функции без аргументов) не более limit одновременно.
 * Возвращает промис с массивом результатов в порядке задач.
 */
export async function promisePool(tasks, limit) {
  // TODO: сейчас все задачи стартуют сразу и лимит игнорируется
  return Promise.all(tasks.map((task) => task()));
}

/**
 * Вызывает task(signal). Если за ms миллисекунд результата нет - abort() и отказ
 * ошибкой с name === "TimeoutError". Таймер всегда останавливается.
 */
export function withTimeout(task, ms) {
  // TODO: добавьте AbortController, таймер и Promise.race
  return task(new AbortController().signal);
}
