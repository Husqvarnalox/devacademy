import { fileURLToPath } from 'node:url';

export function greet(name) {
  return `Hello, ${name}!`;
}

export function main(argv) {
  console.log(greet(argv[0] ?? 'world'));
}

// Запускаем только при «node main.js», но не при импорте из теста.
if (process.argv[1] === fileURLToPath(import.meta.url)) {
  main(process.argv.slice(2));
}
