// Пять функций на методах строк. Слово export делает функцию видимой для проверки.

/**
 * Слово с заглавной первой буквой, остальные буквы строчные.
 * capitalize("иВАН") -> "Иван". Для "" - "".
 */
export function capitalize(word) {
  return word.slice(0, 1).toUpperCase() + word.slice(1).toLowerCase();
}

/**
 * Число слов. Слова разделены одним пробелом, по краям могут быть лишние пробелы.
 * countWords("  привет мир ") -> 2. Для "" и "   " - 0.
 */
export function countWords(text) {
  const clean = text.trim();
  if (clean === "") {
    return 0;
  }
  return clean.split(" ").length;
}

/**
 * Заголовок для адреса страницы: без краевых пробелов, строчными буквами, пробелы -> "-".
 * slugify("  Мой Первый Пост ") -> "мой-первый-пост".
 */
export function slugify(title) {
  return title.trim().toLowerCase().replaceAll(" ", "-");
}

/**
 * Если text.length <= max - вернуть text; иначе первые max символов и "...".
 * shorten("Длинный заголовок", 7) -> "Длинный...".
 */
export function shorten(text, max) {
  if (text.length <= max) {
    return text;
  }
  return text.slice(0, max) + "...";
}

/**
 * true, если word встречается в text без учёта регистра.
 * mentions("Купить МОЛОКО", "молоко") -> true.
 */
export function mentions(text, word) {
  return text.toLowerCase().includes(word.toLowerCase());
}
