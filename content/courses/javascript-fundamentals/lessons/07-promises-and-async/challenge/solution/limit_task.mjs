// mapLimit: параллельный обход с ограничением числа одновременных задач.

/**
 * mapLimit(items, limit, fn) - применяет async-функцию fn(item, index) ко всем элементам так,
 * чтобы одновременно выполнялось не больше limit вызовов. Результаты - в исходном порядке.
 * При первой ошибке итоговый промис отклоняется ею, новые вызовы больше не запускаются.
 * limit должен быть целым числом не меньше 1, иначе промис отклоняется RangeError.
 */
export async function mapLimit(items, limit, fn) {
  if (!Number.isInteger(limit) || limit < 1) {
    throw new RangeError("limit должен быть целым числом не меньше 1");
  }
  const list = [...items];
  const results = new Array(list.length);
  let next = 0;
  let failed = false;

  async function worker() {
    while (!failed && next < list.length) {
      const index = next++;
      try {
        results[index] = await fn(list[index], index);
      } catch (error) {
        failed = true;
        throw error;
      }
    }
  }

  await Promise.all(Array.from({ length: Math.min(limit, list.length) }, worker));
  return results;
}
