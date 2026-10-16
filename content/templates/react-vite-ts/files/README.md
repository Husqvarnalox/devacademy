# {{name}}

Приложение на React 19 + Vite 8 + TypeScript с тестами Vitest и Testing Library.
Все версии в `package.json` закреплены точно.

## Что внутри

- `src/main.tsx` — точка входа, монтирует `<App />` в `#root`.
- `src/App.tsx` — компонент со счётчиком.
- `src/App.test.tsx` — тест компонента (Testing Library + user-event, окружение jsdom).
- `src/setupTests.ts` — подключает матчеры `jest-dom` (`toBeInTheDocument` и т. д.).
- `vite.config.ts` — один конфиг для Vite и Vitest (`vitest/config`).
- `tsconfig.json` — строгий режим, `noEmit`: TypeScript только проверяет типы, код собирает Vite.

## Скрипты

| Команда | Что делает |
| --- | --- |
| `npm install` | ставит зависимости (нужна сеть) |
| `npm run dev` | dev-сервер Vite, по умолчанию http://localhost:5173 |
| `npm test` | один прогон тестов Vitest |
| `npm run test:watch` | тесты в режиме наблюдения |
| `npm run typecheck` | только проверка типов |
| `npm run build` | проверка типов и production-сборка в `dist/` |
| `npm run preview` | локальный просмотр собранного `dist/` |

## Заметки об архитектуре

- Компоненты тестируйте через поведение пользователя (`getByRole`, `userEvent`), а не через детали реализации.
- `globals: true` в конфиге позволяет писать `describe/it/expect` без импорта; типы подключены в `tsconfig.json`.
- Для запросов к API добавьте слой `src/api/` и подменяйте его в тестах, а не мокайте `fetch` в каждом компоненте.
