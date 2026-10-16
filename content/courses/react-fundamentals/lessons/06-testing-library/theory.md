<!-- section: simple -->
## Тестируем то, что видит пользователь

Как проверить компонент? Можно заглядывать в его состояние и вызывать внутренние функции, но такие тесты ломаются при любом рефакторинге, хотя поведение не меняется. **Testing Library** предлагает другой принцип: *чем больше тесты похожи на то, как программой пользуются люди, тем больше им можно доверять*. Тест рисует компонент, находит элементы так, как это делает человек (по роли, подписи, тексту), совершает действия (клик, ввод) и проверяет, что изменилось на экране.

```jsx
import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";

it("раскрывает секцию по клику", async () => {
  const user = userEvent.setup();
  render(<Accordion title="Доставка">Курьером</Accordion>);

  expect(screen.queryByText("Курьером")).toBeNull();
  await user.click(screen.getByRole("button", { name: "Доставка" }));
  expect(screen.getByText("Курьером")).toBeTruthy();
});
```

Здесь три роли: `render` создаёт компонент в DOM (в курсе - в имитирующем браузер jsdom); `screen` содержит запросы, а `userEvent` совершает действия. В запросах `name` - **доступное имя** элемента, то есть то, как его озвучит экранный диктор: текст кнопки, подпись поля.

Запросы Testing Library работают на любом DOM, не только на реакте. Чтобы увидеть их без браузера и тестового раннера, построим DOM в jsdom из разметки, которую даёт сервер-рендер, и применим запросы:

```javascript run
const { JSDOM } = require("jsdom");
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const dom = require("@testing-library/dom");
const h = React.createElement;

const html = renderToStaticMarkup(
  h("form", null,
    h("label", null, "Имя", h("input", { defaultValue: "" })),
    h("button", { type: "submit" }, "Найти"),
    h("h3", null, h("button", { type: "button", "aria-expanded": false }, "Доставка")),
  ),
);
const body = new JSDOM(`<body>${html}</body>`).window.document.body;

console.log(dom.getByLabelText(body, "Имя").tagName);
console.log(dom.getByRole(body, "button", { name: "Найти" }).textContent);
console.log(dom.getByRole(body, "button", { name: "Доставка", expanded: false }).textContent);
console.log(dom.getByRole(body, "heading").textContent);
console.log(dom.queryByText(body, "Нет такого"));
```
```output
INPUT
Найти
Доставка
Доставка
null
```

Поле находится по подписи `<label>`, кнопки - по роли и имени, заголовок - по роли `heading`. Опция `expanded: false` проверяет атрибут `aria-expanded`. Если запрос не находит элемент, `queryBy...` возвращает `null`.

<!-- section: precise -->
## Точные правила

### Запросы и приоритет

Три семейства запросов отличаются поведением при ненайденном элементе:

| Запрос | Нет элемента | Один | Несколько | Асинхронный |
| --- | --- | --- | --- | --- |
| `getBy...` | ошибка | элемент | ошибка | нет |
| `queryBy...` | `null` | элемент | ошибка | нет |
| `findBy...` | ошибка после таймаута | элемент | ошибка | да (промис) |

Для множества элементов есть варианты `getAllBy...`, `queryAllBy...`, `findAllBy...`, возвращающие массив. Используйте `getBy` для утверждения «элемент есть», `queryBy` - для «элемента нет», `findBy` - для «элемент скоро появится».

Порядок предпочтения (от лучшего к худшему) такой: `getByRole` (с `name`), `getByLabelText`, `getByPlaceholderText`, `getByText`, `getByDisplayValue`, затем `getByAltText`, `getByTitle` и в самом конце `getByTestId`. Чем выше запрос, тем ближе он к тому, как элемент находит человек, и тем лучше он проверяет доступность. Селекторы по классам и структуре (`querySelector`) почти всегда ломаются при вёрстке.

```javascript run
const { JSDOM } = require("jsdom");
const dom = require("@testing-library/dom");

const { document } = new JSDOM(`<body><ul><li>Аня</li><li>Борис</li></ul><button>Найти</button></body>`).window;
const body = document.body;

console.log(dom.getAllByRole(body, "listitem").length);
console.log(dom.queryByText(body, "Вера"));
console.log(dom.queryAllByRole(body, "link").length);
try {
  dom.getByRole(body, "listitem");
} catch (error) {
  console.log(error.message.split("\n")[0]);
}
try {
  dom.getByText(body, "Вера");
} catch (error) {
  console.log(error.message.split(":")[0]);
}
```
```output
2
null
0
Found multiple elements with the role "listitem"
Unable to find an element with the text
```

`getBy` с несколькими совпадениями тоже падает: если элементов много, нужен `getAllBy` или более точный запрос.

### Действия: user-event

`userEvent.setup()` создаёт «пользователя». Его методы - `click`, `type`, `keyboard`, `tab`, `selectOptions` - воспроизводят всю цепочку настоящих событий (наведение, фокус, нажатие клавиш, `input`, `change`), поэтому ближе к жизни, чем `fireEvent.click`. Все методы **асинхронны**: ставьте `await`. Специальные клавиши пишут в фигурных скобках: `await user.type(field, "Аня{enter}")`.

`fireEvent` из Testing Library отправляет одно событие и пригодится, когда нужно ровно оно.

### Асинхронность: findBy, waitFor, act

Если интерфейс меняется после промиса или таймера, элемента нет сразу после действия. Тогда ждут: `await screen.findByText("Готово")`. `findBy` - это `getBy`, который повторяется в `waitFor` (по умолчанию до 1000 мс, шаг 50 мс). `waitFor` принимает колбэк с проверкой, который повторяется, пока не перестанет бросать ошибку:

```javascript run
const { JSDOM } = require("jsdom");
const dom = require("@testing-library/dom");

const { document } = new JSDOM("<body></body>").window;
const body = document.body;

setTimeout(() => {
  const p = document.createElement("p");
  p.textContent = "Готово";
  body.append(p);
}, 50);

async function main() {
  console.log(dom.queryByText(body, "Готово"));
  const found = await dom.findByText(body, "Готово");
  console.log(found.textContent);

  let attempts = 0;
  await dom.waitFor(() => {
    attempts += 1;
    if (attempts < 3) throw new Error("ещё нет");
  }, { container: body, interval: 10 });
  console.log("попыток:", attempts);
}
main();
```
```output
null
Готово
попыток: 3
```

Сразу после запуска элемента нет (`null`), но `findByText` дождался его появления. `waitFor` повторил проверку три раза.

**`act`** - функция React, которая даёт обновлениям состояния и эффектам завершиться до продолжения теста. Testing Library оборачивает в `act` свои `render`, `fireEvent`, действия `user-event` и `waitFor`, так что вручную вызывать его почти не приходится. Предупреждение «not wrapped in act(...)» означает, что состояние обновилось вне тех обёрток: обычно тест не дождался результата (забыт `await` или `findBy`).

### Что проверять

Проверяйте результат для пользователя: текст на экране, наличие или отсутствие элемента, `disabled`, `aria-expanded`, значение поля, вызванные колбэки (`vi.fn()`) и их аргументы. Не проверяйте внутреннее состояние, имена внутренних функций и количество рендеров. Внешние зависимости (запрос, таймер) подменяйте: передавайте функцию как prop или используйте `vi.fn()`.

<!-- section: examples -->
## Примеры

**Пример 1. Форма: ввод и результат.**

```jsx
it("ищет по введённому имени", async () => {
  const user = userEvent.setup();
  const search = vi.fn(() => Promise.resolve(["Аня"]));
  render(<SearchUsers search={search} />);

  await user.type(screen.getByLabelText("Имя"), "Ан");
  await user.click(screen.getByRole("button", { name: "Найти" }));

  expect(await screen.findByText("Аня")).toBeTruthy();
  expect(search).toHaveBeenCalledWith("Ан");
});
```

`getByLabelText` находит поле через `<label>`, `type` печатает посимвольно, `findByText` ждёт ответа.

**Пример 2. Отсутствие и состояние загрузки.**

```jsx
await user.click(screen.getByRole("button", { name: "Найти" }));
expect(screen.getByRole("status").textContent).toBe("Поиск...");
await waitFor(() => expect(screen.queryByRole("status")).toBeNull());
```

Для исчезновения используют `waitFor` с `queryBy`: `findBy` проверяет появление, а не исчезновение. Есть и `waitForElementToBeRemoved`.

**Пример 3. Управляемый ответ вместо сети.** Чтобы тест сам решал, когда придёт ответ, делают «отложенный» промис:

```jsx
function deferred() {
  let resolve;
  const promise = new Promise((res) => { resolve = res; });
  return { promise, resolve };
}
const pending = deferred();
render(<SearchUsers search={() => pending.promise} />);
// ...действие...
pending.resolve(["Аня"]); // теперь ответ приходит
```

Так без реальных таймеров и сети проверяют промежуточные состояния: «загрузка», «ошибка», гонки.

**Пример 4. Область поиска.** Когда на странице несколько одинаковых блоков, сужают область: `const panel = screen.getByRole("region", { name: "Корзина" }); within(panel).getByRole("button")`.

**Пример 5. Клавиатура.** `await user.tab()` перемещает фокус, `await user.keyboard("{Enter}")` нажимает клавишу. Если ваш элемент - настоящий `<button>`, Enter и пробел работают бесплатно; для `<div onClick>` пришлось бы писать это самому.

<!-- section: mistakes -->
## Типичные ошибки

* **Забытый `await`.** `user.click(...)` без `await` возвращает промис, и проверка выполняется до действия. Сразу видно по предупреждению про `act` или падающей проверке.
* **`getBy` для проверки отсутствия.** `expect(screen.getByText("x")).toBeNull()` упадёт раньше проверки: `getBy` бросает ошибку. Для отсутствия нужен `queryBy`.
* **`getBy` вместо `findBy` для асинхронного.** Элемент ещё не появился, запрос падает. Ожидайте через `findBy` или `waitFor`.
* **Побочные действия внутри `waitFor`.** Колбэк повторяется много раз: клик внутри него выполнится много раз. Внутри только проверка.
* **`getByTestId` по привычке.** Он не проверяет, что видит пользователь. Сначала роль и подпись; `data-testid` - крайняя мера.
* **Проверка реализации.** Тест, который читает состояние или считает рендеры, ломается при рефакторинге. Проверяйте результат на экране.
* **Фиксированные паузы.** `await new Promise(r => setTimeout(r, 500))` делает тест медленным и нестабильным; пользуйтесь `findBy` и `waitFor`.
* **Утверждения без проверки роли.** `getByText("Сохранить")` найдёт и заголовок, и кнопку. Для кнопки пишите `getByRole("button", { name: "Сохранить" })`.
* **Нет очистки между тестами.** Testing Library убирает отрисованное сама после каждого теста, если тестовый раннер предоставляет хуки `afterEach` (в курсе это настроено); остальное окружение (`document.title`, шпионы `vi.spyOn`) очищайте вручную.

<!-- section: summary -->
## Итоги

* Testing Library проверяет поведение глазами пользователя: роль, подпись, текст, а не состояние и внутренние функции.
* Приоритет запросов: `getByRole` с `name`, затем `getByLabelText`, `getByText`, а `getByTestId` - в крайнем случае.
* `getBy` - элемент должен быть, `queryBy` - может не быть (`null`), `findBy` - появится позже (промис).
* `userEvent.setup()` и `await user.click / type / keyboard` воспроизводят настоящие цепочки событий.
* Асинхронные результаты ждут через `findBy` и `waitFor`; `act` Testing Library вызывает за вас.
* Внешние зависимости подменяют (`vi.fn()`, отложенные промисы), а не обращаются к сети и таймерам.
