// Геометрия на плоскости. Добавьте типы так, чтобы код проходил tsc --strict, а функции работали.
// Подсказка: точку описывает форма { x: number; y: number }; её удобно назвать один раз.

export function distance(a, b) {
  // TODO: евклидово расстояние между a и b
  return 0;
}

export function centroid(points) {
  // TODO: средняя точка. Для пустого списка вернуть undefined
  return undefined;
}

export function translate(p, dx, dy) {
  // TODO: новая точка, сдвинутая на (dx, dy); исходную не менять
  return p;
}

export function nearest(points, target) {
  // TODO: ближайшая к target точка из points или undefined, если points пуст
  return undefined;
}
