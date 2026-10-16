<!-- section: simple -->
## Props: данные от родителя к потомку

Компонент - функция, а **props** - её единственный аргумент: объект со всем, что родитель передал в атрибутах JSX. Через props данные текут вниз по дереву: от родителя к потомку.

```jsx
function Badge({ text, tone = "neutral" }) {
  return <span data-tone={tone}>{text}</span>;
}

<Badge text="Новое" />                 // tone равен "neutral"
<Badge text="Срочно" tone="danger" />
```

Объект props разбирают деструктуризацией прямо в параметрах, а **значение по умолчанию** задают там же: `tone = "neutral"`. Props **только для чтения**: компонент не вправе их менять. Чтобы что-то могло меняться со временем, нужно состояние - это тема следующего урока.

Особый prop - **`children`**: всё, что написано между открывающим и закрывающим тегом компонента.

```jsx
<Card title="Заказ">
  <p>Два товара</p>
  <Badge text="Оплачен" />
</Card>
```

Компонент `Card` сам решает, где выведет `children`. Так делают обёртки: карточки, панели, окна. Это и есть **композиция** - сборка интерфейса из вложенных компонентов.

Посмотрите, как выглядят props у элементов. Запускаемые примеры - обычные скрипты Node.js, поэтому JSX заменён вызовами `createElement`.

```javascript run
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;

function Badge({ text, tone = "neutral" }) {
  return h("span", { "data-tone": tone }, text);
}

console.log(renderToStaticMarkup(h(Badge, { text: "Новое" })));
console.log(renderToStaticMarkup(h(Badge, { text: "Срочно", tone: "danger" })));
```
```output
<span data-tone="neutral">Новое</span>
<span data-tone="danger">Срочно</span>
```

<!-- section: precise -->
## Точные правила

### Props и значения по умолчанию

* Props - один объект. Запись `function Badge({ text, tone })` - просто деструктуризация первого параметра.
* Значение по умолчанию в деструктуризации срабатывает только для `undefined`. Если родитель передал `null`, останется `null`.
* Props неизменяемы: React замораживает объект (в режиме разработки), поэтому присваивание в `props.x` не сработает, а вложенные объекты принадлежат родителю и менять их тоже нельзя.
* В React 19 у функциональных компонентов нет `propTypes` и `defaultProps`: значения по умолчанию задают параметрами, типы описывают TypeScript или другим инструментом.

```javascript run
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;

function Badge({ text, tone = "neutral" }) {
  return h("span", tone === null ? null : { "data-tone": tone }, text);
}

console.log(renderToStaticMarkup(h(Badge, { text: "a", tone: undefined })));
console.log(renderToStaticMarkup(h(Badge, { text: "b", tone: null })));
console.log(Object.isFrozen(h(Badge, { text: "c" }).props));
```
```output
<span data-tone="neutral">a</span>
<span>b</span>
true
```

`undefined` даёт значение по умолчанию, `null` - нет (здесь мы сами его обработали). Объект props элемента заморожен.

### children и слоты

Если у элемента один дочерний узел, `props.children` - он сам. Если несколько - массив. Если ни одного - свойство не задано. Кроме `children`, в props можно передавать **любые элементы**, получая несколько «слотов»: `<Card title="..." footer={<Button />}>`.

```javascript run
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;

function Card({ title, children, footer }) {
  return h("section", null, h("h2", null, title), h("div", null, children), footer ? h("footer", null, footer) : null);
}

const two = h(Card, { title: "T" }, h("p", null, "a"), h("p", null, "b"));
const one = h(Card, { title: "T" }, h("p", null, "a"));
console.log(Array.isArray(two.props.children), two.props.children.length);
console.log(Array.isArray(one.props.children), "children" in h(Card, { title: "T" }).props);
console.log(renderToStaticMarkup(h(Card, { title: "Заказ", footer: "Итого: 5" }, h("p", null, "Чай"))));
```
```output
true 2
false false
<section><h2>Заказ</h2><div><p>Чай</p></div><footer>Итого: 5</footer></section>
```

### Композиция вместо наследования

В React не строят иерархии «Button -> PrimaryButton -> DangerButton». Различия задают props, а общий каркас оформляют через `children` и слоты. Компонент может принимать и другой компонент как prop, и функцию, которую вызовет сам (это пригодится в уроке про события).

### Ключи списков

Когда вы выводите массив элементов, каждому нужен **`key`** - строка или число, уникальные среди соседей. Ключ - это **идентичность** элемента между рендерами. Если ключ тот же, React считает, что это тот же элемент: сохраняет его состояние и узел DOM, а новые props лишь обновляет. Если ключа нет в новом списке, элемент удаляется вместе с состоянием.

* `key` не является prop: компонент его не получит (`props.key` равен `undefined`). Ключ лежит в самом элементе.
* Ключ уникален только среди **соседей**, глобальной уникальности не нужно.
* Подходящий ключ - стабильный идентификатор данных (id записи).
* **Индекс** годится только для списка, который никогда не переставляют, не фильтруют и не меняют в середине.
* `Math.random()` в key делает каждый рендер новым элементом: состояние и фокус теряются при каждом обновлении.

```javascript run
const React = require("react");
const h = React.createElement;

const items = [{ id: "a", t: "A" }, { id: "b", t: "B" }];
const withKeys = items.map((i) => h("li", { key: i.id }, i.t));
console.log(withKeys.map((el) => el.key).join(","));
console.log(h("li", null, "x").key);
console.log(typeof h("li", { key: 7 }, "x").key);
```
```output
a,b
null
string
```

Число в `key` приводится к строке, а без ключа поле `key` равно `null`.

### Почему индекс ломает состояние

Пусть список полей ввода использует `key={index}`, и пользователь вписал текст в поле для заметки `a`. Данные поменяли: `[a, b]` стали `[b, a]`. Для React ключ `0` по-прежнему тот же элемент, поэтому он **переиспользует то же поле с тем же состоянием**, но в него приходит заголовок заметки `b`. Текст, который относился к `a`, оказывается у `b`. С `key={note.id}` React находит элемент с ключом `a` на новом месте и переносит его вместе с полем.

<!-- section: examples -->
## Примеры

**Пример 1. Пустое значение и условный слот.**

```jsx
function Alert({ title, children, action }) {
  return (
    <div role="alert">
      <strong>{title}</strong>
      {children}
      {action}
    </div>
  );
}

<Alert title="Ошибка" action={<button>Повторить</button>}>
  Не удалось сохранить.
</Alert>
```

`action` - слот: родитель передаёт готовый элемент, а `Alert` решает, где его показать. Если `action` не передан, на его месте ничего не будет.

**Пример 2. Список с настоящим ключом.**

```javascript run
const React = require("react");
const { renderToStaticMarkup } = require("react-dom/server");
const h = React.createElement;

function Tags({ tags }) {
  return h("ul", null, tags.map((tag) => h("li", { key: tag.id }, tag.name)));
}

const tags = [{ id: 10, name: "react" }, { id: 11, name: "jsx" }];
console.log(renderToStaticMarkup(h(Tags, { tags })));
console.log(renderToStaticMarkup(h(Tags, { tags: [...tags].reverse() })));
```
```output
<ul><li>react</li><li>jsx</li></ul>
<ul><li>jsx</li><li>react</li></ul>
```

Разметка не показывает ключи, но они определяют, какие элементы React переиспользует при обновлении.

**Пример 3. Когда индекс допустим.**

```jsx
const steps = ["Корзина", "Адрес", "Оплата"];
<ol>{steps.map((s, i) => <li key={i}>{s}</li>)}</ol>
```

Список постоянный, его не переставляют и в нём нет состояния у элементов, поэтому индекс безопасен. Как только появятся поля ввода, сортировка или удаление, берите id.

**Пример 4. Передача id вместо key.** Если потомку нужен идентификатор, передайте его отдельным prop: `<Row key={n.id} id={n.id} />`. Ключ внутри `Row` прочитать нельзя.

<!-- section: mistakes -->
## Типичные ошибки

* **Изменение props.** `props.items.sort()` меняет массив родителя; `props.title = "x"` вызывает ошибку. Копируйте: `[...props.items].sort(...)`.
* **`key={index}` в изменяемом списке.** Поля ввода, фокус, раскрытые панели «переезжают» к другим данным при сортировке и удалении.
* **`key={Math.random()}`.** Элемент пересоздаётся при каждом рендере: пропадает состояние и фокус, работа тратится впустую.
* **Ключ на внутреннем элементе вместо внешнего.** Ключ нужен самому внешнему элементу, который возвращает `map`: `items.map(i => <Row key={i.id} />)`, а не ключ у элемента внутри `Row`.
* **Чтение `props.key`.** Значение `undefined` и предупреждение в консоли. Передавайте идентификатор отдельным prop.
* **Лишнее копирование props в состояние.** `useState(props.value)` фиксирует значение на первом рендере и перестаёт отслеживать родителя. Если данные приходят сверху, используйте их напрямую.
* **Условие `&&` с числом.** `{count && <List />}` при `count` равном 0 выведет `0`: сравнивайте явно, `count > 0 && ...`.

<!-- section: summary -->
## Итоги

* Props - объект с данными от родителя; он только для чтения, значения по умолчанию задают в параметрах (в React 19 нет `defaultProps` у функциональных компонентов).
* `children` и props-слоты строят композицию: общий каркас в одном компоненте, различия в данных и содержимом.
* `key` - идентичность элемента между рендерами, а не способ убрать предупреждение; он не доступен как prop.
* Ключ должен быть уникальным среди соседей и стабильным: id данных. Индекс допустим только для постоянных списков без состояния.
* Индекс как ключ при перестановке и удалении приводит к тому, что состояние остаётся у позиции, а не у данных.
