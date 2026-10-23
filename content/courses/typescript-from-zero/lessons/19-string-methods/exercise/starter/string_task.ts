// Четыре функции для работы с текстом. Слово export делает функцию видимой для проверки.

// Слово с заглавной первой буквой, остальные буквы строчные. Для пустой строки результат "".
// capitalize("пРИВЕТ") = "Привет"
export function capitalize(word: string): string {
  // TODO: slice(0, 1).toUpperCase() + остаток в нижнем регистре
  return "?";
}

// Инициалы: первые буквы всех слов, заглавными, без разделителей. Слова разделены пробелами,
// пробелов может быть больше одного, в том числе по краям.
// initials("  иван  петрович  сидоров ") = "ИПС"; initials("") = ""
export function initials(fullName: string): string {
  // TODO: trim, split(" "), отбросьте пустые части и склейте первые буквы
  return "?";
}

// Адрес страницы из заголовка: без пробелов по краям, строчными буквами, пробелы заменены на "-".
// Считайте, что внутри заголовка слова разделены ровно одним пробелом.
// slugify("  Hello World  ") = "hello-world"
export function slugify(title: string): string {
  // TODO: trim, toLowerCase, replaceAll
  return "?";
}

// Сколько слов в тексте. Слова разделены пробелами, пробелов может быть много, в том числе по краям.
// countWords("  один  два три ") = 3; countWords("   ") = 0
export function countWords(text: string): number {
  // TODO: trim, split(" "), отбросьте пустые части, верните длину
  return -1;
}
