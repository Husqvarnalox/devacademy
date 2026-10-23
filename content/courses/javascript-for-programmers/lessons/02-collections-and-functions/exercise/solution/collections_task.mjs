// Четыре функции о массивах, ссылках и словарях.

export function sortedNumbers(values) {
  return [...values].sort((x, y) => x - y);
}

export function countWords(words) {
  const counts = new Map();
  for (const w of words) {
    counts.set(w, (counts.get(w) ?? 0) + 1);
  }
  return counts;
}

export function sameItems(a, b) {
  if (!Array.isArray(a) || !Array.isArray(b)) return false;
  if (a.length !== b.length) return false;
  for (let i = 0; i < a.length; i++) {
    const same = a[i] === b[i] || (Number.isNaN(a[i]) && Number.isNaN(b[i]));
    if (!same) return false;
  }
  return true;
}

export function average(values) {
  if (values.length === 0) return null;
  return values.reduce((sum, v) => sum + v, 0) / values.length;
}
