// Три функции для обработки массивов. Исходные данные менять нельзя.

/** Имена игроков с score >= minScore: по убыванию очков, при равенстве - по алфавиту. */
export function topScorers(players, minScore) {
  return players.sort((a, b) => b.score - a.score).map((p) => p.name);
}

/** Новый массив без повторов, порядок первого появления, сравнение строгое. */
export function unique(values) {
  return values.filter((v, i) => values.indexOf(v) === i && v);
}

/** Объект: категория -> сумма amount. */
export function totalByCategory(items) {
  const totals = {};
  for (const item of items) {
    totals[item.category] = item.amount;
  }
  return totals;
}
