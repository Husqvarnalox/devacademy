export function topScorers(players, minScore) {
  return players
    .filter((p) => p.score >= minScore)
    .sort((a, b) => b.score - a.score || (a.name < b.name ? -1 : a.name > b.name ? 1 : 0))
    .map((p) => p.name);
}

export function unique(values) {
  return [...new Set(values)];
}

export function totalByCategory(items) {
  const totals = {};
  for (const { category, amount } of items) {
    totals[category] = (totals[category] ?? 0) + amount;
  }
  return totals;
}
