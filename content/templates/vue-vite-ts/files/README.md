# {{name}}

Приложение на Vue 3 (Composition API, `<script setup>`) + Vite 8 + TypeScript с тестами Vitest и Vue Test Utils.
Все версии в `package.json` закреплены точно.

## Что внутри

- `src/main.ts` — создаёт приложение и монтирует его в `#app`.
- `src/App.vue` — компонент со счётчиком.
- `src/App.test.ts` — тест компонента (`@vue/test-utils`, окружение jsdom).
- `src/env.d.ts` — типы для импорта `*.vue` и `vite/client`.
- `vite.config.ts` — один конфиг для Vite и Vitest.

## Скрипты

| Команда | Что делает |
| --- | --- |
| `npm install` | ставит зависимости (нужна сеть) |
| `npm run dev` | dev-сервер Vite, по умолчанию http://localhost:5173 |
| `npm test` | один прогон тестов Vitest |
| `npm run test:watch` | тесты в режиме наблюдения |
| `npm run typecheck` | проверка типов, включая шаблоны `.vue` (`vue-tsc`) |
| `npm run build` | проверка типов и production-сборка в `dist/` |
| `npm run preview` | локальный просмотр собранного `dist/` |

## Заметки об архитектуре

- Здесь TypeScript закреплён на 6.0.3, а не на 7: `vue-tsc` пока использует JavaScript API компилятора, которого нет в TypeScript 7.
- Тестируйте компоненты через `mount` и публичный интерфейс (props, события, отрисованный текст).
- Общее состояние выносите в composables (`src/composables/`), их проще тестировать отдельно от компонентов.
