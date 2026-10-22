// Три небольшие функции. Слово export делает функцию видимой для проверки.

// Возвращает приветствие вида "Привет, Анна!"
export function greet(name) {
  return "Привет, " + name + "!";
}

// Возвращает площадь прямоугольника
export function rectangleArea(width, height) {
  return width * height;
}

// Возвращает среднее арифметическое двух чисел
export function average(a, b) {
  return (a + b) / 2;
}
