/**
 * Применяет асинхронную fn(item, index, signal) к элементам, не запуская одновременно больше limit вызовов.
 * Результаты возвращаются в порядке элементов. Подробности - в условии урока.
 */
export async function mapLimit(items, limit, fn, { signal } = {}) {
  // TODO: сейчас все вызовы стартуют сразу, лимит и отмена не учитываются.
  const controller = new AbortController();
  return Promise.all([...items].map((item, index) => fn(item, index, controller.signal)));
}
