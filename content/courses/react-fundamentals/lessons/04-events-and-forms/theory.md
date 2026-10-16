<!-- section: simple -->
## События: пользователь действует, интерфейс отвечает

Пользователь нажимает кнопки, вводит текст, отправляет формы. В React обработчики событий - это обычные функции, которые передают в **props** элемента: `onClick`, `onChange`, `onSubmit`. Названия записываются в camelCase.

```jsx
function SaveButton({ onSave }) {
  return <button type="button" onClick={() => onSave("черновик")}>Сохранить</button>;
}
```

Обработчик - это **функция**, которую React вызовет позже. Частая ошибка - вызвать её сразу: `onClick={save()}` запустит `save` во время рендера, а в `onClick` попадёт результат. Правильно: `onClick={save}` или `onClick={() => save(id)}`.

В обработчик первым аргументом приходит объект события. Через него читают, что произошло:

* `event.target.value` - текст в поле ввода;
* `event.preventDefault()` - отмена стандартного действия браузера (для формы это отправка и перезагрузка страницы).

Обработчики видны в объекте элемента как обычные функции, но в разметку для сервера они не попадают:

```javascript run
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;

const log = [];
const button = h("button", { type: "button", onClick: (e) => log.push(e.type) }, "OK");

console.log(typeof button.props.onClick);
button.props.onClick({ type: "click" }); // вызываем вручную, как это сделал бы React
console.log(log);
console.log(renderToStaticMarkup(button));
```
```output
function
[ 'click' ]
<button type="button">OK</button>
```

React вызывает функцию из `onClick` при настоящем клике, а в HTML-строке атрибута `onclick` нет: подписки на события оформляет клиентский React.

<!-- section: precise -->
## Точные правила

### Обработчики и событие

* Обработчик - функция в props: `onClick`, `onChange`, `onSubmit`, `onKeyDown` и т. д. Имя события пишется в camelCase, значение - функция, а не строка.
* Аргументы обработчика: объект события. Чтобы передать свои данные, оборачивают в стрелку: `onClick={() => remove(item.id)}`.
* Объект события React - обёртка над нативным (синтетическое событие), у неё те же `preventDefault()`, `stopPropagation()`, `target`.
* События по умолчанию **всплывают** от элемента к родителям; `stopPropagation()` останавливает всплытие, `preventDefault()` отменяет стандартное действие браузера. Это разные вещи.
* **`onChange` у `<input>` срабатывает на каждое изменение**, а не при потере фокуса (как нативный `input`).
* Обработчик может менять состояние, вызывать колбэки из props, делать запросы. Тело компонента для побочных действий не предназначено, а обработчик - именно то место.

### Управляемые поля

Поле ввода **управляется**, когда его значение задаёт состояние React.

```jsx
function NameField() {
  const [name, setName] = useState("");
  return (
    <label>
      Имя
      <input value={name} onChange={(e) => setName(e.target.value)} />
    </label>
  );
}
```

Цикл такой: пользователь вводит символ -> срабатывает `onChange` -> вы кладёте `e.target.value` в состояние -> React перерисовывает и ставит в поле это значение. **Источник истины - состояние**, а не DOM: если не обновить состояние, введённый символ не появится, потому что React вернёт поле к `value`.

Альтернатива - **неуправляемое** поле: `<input defaultValue="..." />` хранит значение само, читают его при отправке (из формы или через `ref`). Такой вариант проще для маленьких форм, но им труднее управлять (валидация «на лету», сброс, зависимые поля).

| Элемент | Что задаёт значение | Откуда читать |
| --- | --- | --- |
| `<input>`, `<textarea>`, `<select>` | `value` | `e.target.value` |
| `<input type="checkbox">` | `checked` | `e.target.checked` |

Различие видно в разметке: `value` и `defaultValue` дают одинаковый HTML, но по-разному ведут себя после первого рендера. Для `textarea` и `select` React тоже использует `value`, а не содержимое и не `selected`:

```javascript run
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;
const noop = () => {};

console.log(renderToStaticMarkup(h("input", { value: "чай", onChange: noop })));
console.log(renderToStaticMarkup(h("input", { defaultValue: "чай" })));
console.log(renderToStaticMarkup(h("input", { type: "checkbox", checked: true, onChange: noop })));
console.log(renderToStaticMarkup(
  h("select", { value: "b", onChange: noop }, h("option", { value: "a" }, "A"), h("option", { value: "b" }, "B")),
));
```
```output
<input value="чай"/>
<input value="чай"/>
<input type="checkbox" checked=""/>
<select><option value="a">A</option><option value="b" selected="">B</option></select>
```

### Формы

Обработчик пишут на `<form onSubmit>`, а не на `onClick` у кнопки: тогда работает и отправка клавишей Enter. В обработчике первым делом вызывают `event.preventDefault()`, потом проверяют данные и передают результат вверх через колбэк из props.

```jsx
function handleSubmit(event) {
  event.preventDefault();
  if (name.trim() === "") { setError("Введите имя"); return; }
  setError("");
  onSubmit({ name: name.trim() });
}
```

Для нескольких полей можно хранить объект и обновлять поле по имени:

```javascript run
const form = { name: "", city: "" };
const update = (state, field, value) => ({ ...state, [field]: value });

const step1 = update(form, "city", "Омск");
const step2 = update(step1, "name", "Аня");
console.log(form, step1, step2);
console.log(form === step1, step1 === step2);
```
```output
{ name: '', city: '' } { name: '', city: 'Омск' } { name: 'Аня', city: 'Омск' }
false false
```

Каждое обновление создаёт новый объект, исходные не меняются - это то самое иммутабельное обновление из прошлого урока, записанное с вычисляемым ключом `[field]`.

### Поднятие состояния

Если два компонента должны показывать согласованные данные, состояние переносят к их **ближайшему общему родителю** и спускают вниз через props: значение и функцию-обработчик. Так возникает один источник истины. Компонент поля (`SearchBox`) становится «глупым»: он только показывает `value` и сообщает `onChange`, а список и фильтр живут в родителе.

<!-- section: examples -->
## Примеры

**Пример 1. Поле поиска и фильтр.** Состояние хранит только текст запроса, а отфильтрованный список считается при рендере.

```jsx
function FilterableList({ items }) {
  const [query, setQuery] = useState("");
  const shown = items.filter((i) => i.toLowerCase().includes(query.toLowerCase()));
  return (
    <>
      <SearchBox value={query} onChange={setQuery} />
      <ul>{shown.map((i) => <li key={i}>{i}</li>)}</ul>
    </>
  );
}
```

`onChange={setQuery}` работает, потому что `SearchBox` вызывает `onChange(e.target.value)` со строкой. Передача функции-setter'а как колбэка допустима.

**Пример 2. Флажок.**

```jsx
const [agree, setAgree] = useState(false);
<label>
  <input type="checkbox" checked={agree} onChange={(e) => setAgree(e.target.checked)} />
  Согласен
</label>
<button type="submit" disabled={!agree}>Продолжить</button>
```

У флажка читают `checked`, а не `value`.

**Пример 3. Обработчик в списке.** Данные в обработчик передаёт стрелка:

```jsx
{todos.map((t) => (
  <li key={t.id}>
    {t.text}
    <button type="button" onClick={() => remove(t.id)}>Удалить</button>
  </li>
))}
```

**Пример 4. Колбэк вверх.** Потомок не меняет состояние родителя напрямую: он вызывает функцию из props. Проверим этот контракт без браузера:

```javascript run
const React = require("react");
const h = React.createElement;

const received = [];
function Row({ id, onRemove }) {
  return h("button", { type: "button", onClick: () => onRemove(id) }, "Удалить");
}

const row = Row({ id: 7, onRemove: (id) => received.push(id) });
row.props.onClick();
row.props.onClick();
console.log(received);
```
```output
[ 7, 7 ]
```

Функция-компонент здесь вызвана напрямую только ради демонстрации; в приложении её вызывает React.

<!-- section: mistakes -->
## Типичные ошибки

* **Вызов вместо передачи.** `onClick={save(id)}` выполняет `save` на рендере. Нужно `onClick={() => save(id)}`.
* **Забыли `preventDefault`.** Страница перезагружается, состояние теряется.
* **`value` без `onChange`.** Поле «заморожено»: пользователь ничего не может ввести, React выдаёт предупреждение. Нужен `onChange` или `readOnly`; для начального значения без управления - `defaultValue`.
* **`value={undefined}`, затем строка.** Поле «переключается» из неуправляемого в управляемое. Начинайте с пустой строки `""`.
* **Дублирование данных.** Хранить `query` и `filteredItems` значит синхронизировать их вручную. Храните минимум, остальное вычисляйте.
* **Состояние слишком низко.** Если два компонента зависят от одних данных, но каждый хранит их у себя, они расходятся. Поднимите состояние к общему родителю.
* **Чтение `event.target.value` в отложенном коде.** Если нужно значение после `await`, сохраните его в переменную до паузы.
* **Путаница `stopPropagation` и `preventDefault`.** Первое останавливает всплытие к родителям, второе - действие браузера по умолчанию.

<!-- section: summary -->
## Итоги

* Обработчики - функции в props (`onClick`, `onChange`, `onSubmit`); передавайте функцию, а не результат вызова.
* `onChange` срабатывает на каждое изменение; значение читают из `event.target.value` (`checked` для флажка).
* В `onSubmit` вызывайте `event.preventDefault()` и обрабатывайте данные сами.
* Управляемое поле: `value` из состояния, `onChange` обновляет состояние; источник истины - состояние.
* Общие данные поднимают к ближайшему общему родителю и передают вниз значение и колбэки.
* Производное (фильтр, счётчики) считают в рендере, а не хранят.
