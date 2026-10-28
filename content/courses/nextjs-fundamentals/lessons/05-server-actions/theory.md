<!-- section: simple -->
## Серверное действие - функция, которую вызывает форма

До сих пор данные в курсе текли в одну сторону: сервер читает и отдаёт страницу. Чтобы изменить данные (добавить заметку, удалить комментарий), нужна обратная дорога от браузера к серверу. В [прошлом уроке](lesson:nextjs.fundamentals.route-handlers) такую дорогу строил обработчик маршрута: вы сами придумывали адрес, метод и формат тела. Для форм в Next.js есть путь короче: **серверное действие** (Server Action).

Серверное действие - это асинхронная функция, которая выполняется на сервере, но подключается к форме как обычный обработчик. Её помечает директива **`"use server"`**:

```ts
// app/notes/actions.ts
"use server";

export async function createNote(formData: FormData) {
  const title = formData.get("title");
  // записать в базу, обновить кэш, перенаправить...
}
```

```tsx
// app/notes/new/page.tsx - серверный компонент, "use client" не нужен
import { createNote } from "../actions";

export default function NewNotePage() {
  return (
    <form action={createNote}>
      <input name="title" />
      <button type="submit">Сохранить</button>
    </form>
  );
}
```

В `action` у формы стоит не адрес, а **функция**: так React 19 расширяет элемент `<form>`. Когда форму отправляют, функция получает объект `FormData` с полями, у которых есть атрибут `name`. Код `createNote` остаётся на сервере: в браузер уходит лишь ссылка на него, а вызов превращается в сетевой запрос методом POST.

Форма работает даже до загрузки JavaScript: это **прогрессивное улучшение** (progressive enhancement). Браузер отправляет её обычным способом, сервер выполняет действие и отвечает, а после гидратации (когда React оживляет страницу) то же происходит без перезагрузки страницы. Это относится к форме в серверном компоненте; в клиентском компоненте отправки до загрузки JS ставятся в очередь.

Что такое `FormData`, показывает модель на чистом TypeScript. Она не импортирует Next.js, но повторяет суть: форма превращается в данные, а функция превращает данные в результат.

```typescript run
type Result = { ok: true; title: string } | { ok: false; error: string };

function createNoteModel(formData: FormData): Result {
  const raw = formData.get("title");
  const title = typeof raw === "string" ? raw.trim() : "";
  return title === "" ? { ok: false, error: "Введите заголовок" } : { ok: true, title };
}

const filled = new FormData();
filled.set("title", "  Купить хлеб ");
console.log(createNoteModel(filled));
console.log(createNoteModel(new FormData()));
```
```output
{ ok: true, title: 'Купить хлеб' }
{ ok: false, error: 'Введите заголовок' }
```

`formData.get` возвращает `FormDataEntryValue | null`: строку, файл или `null`, если поля нет. Поэтому значение проверяют, а не приводят типом `as string`.

<!-- section: precise -->
## Точные правила

### Где ставится директива

* **В начале файла** (`"use server"` первым оператором): все экспортируемые функции файла становятся серверными. Из такого файла можно импортировать действия в клиентские компоненты.
* **Первой строкой тела функции**: помечается одна функция. Так можно объявить действие прямо внутри серверного компонента. В клиентском компоненте объявлять действия нельзя: их импортируют из файла с директивой.

Действие всегда `async`: клиент вызывает его через сеть и ждёт ответа. В документации обе формы называют **серверными функциями** (Server Functions); «серверное действие» - серверная функция, используемая для отправки формы или мутации.

### Что происходит при вызове

Next.js заменяет в клиентском коде реализацию ссылкой (идентификатором действия) и отправляет на сервер запрос **POST**; другим методом действие вызвать нельзя. Клиент выполняет действия по одному: следующее ждёт завершения предыдущего. Если действие обновило кэш (`revalidatePath`, `updateTag`, `refresh`) или вызвало `redirect`, ответ содержит и результат, и заново отрисованный маршрут: второго запроса не нужно.

Проверим идею на модели без Next.js. Реестр сопоставляет идентификатор функции; «сервер» принимает только POST и только известные идентификаторы. Обратите внимание: вызвать функцию можно, и не показав форму.

```typescript run
type Action = (formData: FormData) => Promise<string>;

const registry = new Map<string, Action>([
  ["a1", async (data) => `создана заметка «${data.get("title")}»`],
]);

async function handle(request: Request): Promise<Response> {
  if (request.method !== "POST") return new Response("Method Not Allowed", { status: 405 });
  const id = new URL(request.url).searchParams.get("action") ?? "";
  const action = registry.get(id);
  if (!action) return new Response("Unknown action", { status: 404 });
  return Response.json({ result: await action(await request.formData()) });
}

async function modelMain() {
  const body = new FormData();
  body.set("title", "Позвонить маме");
  const viaPost = await handle(new Request("http://x/notes?action=a1", { method: "POST", body }));
  console.log(viaPost.status, await viaPost.json());
  console.log((await handle(new Request("http://x/notes?action=a1"))).status);
  console.log((await handle(new Request("http://x/notes?action=zz", { method: "POST", body }))).status);
}
modelMain();
```
```output
200 { result: 'создана заметка «Позвонить маме»' }
405
404
```

Вывод для безопасности: у такой функции есть адрес, по которому может прийти запрос от кого угодно, а не только из вашей формы.

### useActionState: результат действия как состояние

Чтобы показать пользователю ошибки проверки, действие **возвращает состояние**, а компонент выводит его. Для этого служит хук `useActionState` из `react`:

```tsx
const [state, formAction, pending] = useActionState(createNote, initialState);
```

* `state` - последнее значение, которое вернуло действие (до первой отправки - `initialState`);
* `formAction` - обёртка вокруг действия, которую передают в `<form action={formAction}>`;
* `pending` - `true`, пока действие выполняется.

Сигнатура действия меняется: **первым аргументом приходит предыдущее состояние**, вторым - `FormData`: `createNote(prevState, formData)`. Хук нужен клиентскому компоненту (`"use client"`), потому что состояние живёт в браузере.

Модель этого цикла: функция получает прежнее состояние и данные, возвращает новое состояние.

```typescript run
type FormState = { error: string | null; values: { title: string } };

async function stepModel(prev: FormState, data: FormData): Promise<FormState> {
  const title = String(data.get("title") ?? "");
  return title.trim() === "" ? { error: "Введите заголовок", values: { title } } : { error: null, values: { title } };
}

async function runSteps() {
  let state: FormState = { error: null, values: { title: "" } };
  for (const typed of ["   ", "Заметка"]) {
    const data = new FormData();
    data.set("title", typed);
    state = await stepModel(state, data);
    console.log(JSON.stringify(state));
  }
}
runSteps();
```
```output
{"error":"Введите заголовок","values":{"title":"   "}}
{"error":null,"values":{"title":"Заметка"}}
```

После выполнения действия React 19 очищает неуправляемые поля формы. Поэтому введённый текст возвращают в состоянии (`values`) и подставляют через `defaultValue`: иначе после ошибки пользователь увидит пустую форму.

### Проверка на сервере и что делать после успеха

* Проверку выполняют **внутри действия**: атрибуты `required` и `type="email"` удобны, но обходятся запросом без формы. Ошибки возвращают как данные, а не бросают: брошенное исключение попадёт в границу ошибок (`error.tsx`).
* После изменения данных обновляют кэш: `revalidatePath("/notes")` (страница) или `revalidateTag`/`updateTag` ([следующий урок](lesson:nextjs.fundamentals.caching)).
* `redirect("/notes/1")` из `next/navigation` переводит пользователя на другую страницу. Функция **бросает исключение** и никогда не возвращает управление: код после неё не выполняется, а вызывать её внутри `try` нельзя, иначе `catch` перехватит управляющее исключение Next.js. Поэтому `revalidatePath` вызывают **до** `redirect`, а `redirect` ставят последним.

Тот же принцип в модели:

```typescript run
class RedirectSignal extends Error {
  readonly to: string;
  constructor(to: string) {
    super("REDIRECT");
    this.to = to;
  }
}
function redirectModel(to: string): never {
  throw new RedirectSignal(to);
}

function wrongWay(): string {
  try {
    redirectModel("/notes/1");
  } catch {
    return "catch перехватил перенаправление";
  }
}

function rightWay(): string {
  try {
    // здесь работа, которая может упасть
  } catch {
    return "ошибка работы";
  }
  redirectModel("/notes/1");
}

console.log(wrongWay());
try {
  rightWay();
} catch (error) {
  console.log(error instanceof RedirectSignal ? `ушли на ${error.to}` : "другая ошибка");
}
```
```output
catch перехватил перенаправление
ушли на /notes/1
```

<!-- section: examples -->
## Примеры

**Пример 1. Действие с проверкой и состоянием.** Файл с директивой, тип состояния и действие, которое возвращает ошибки как данные.

```ts
// app/notes/actions.ts
"use server";

import { revalidatePath } from "next/cache";
import { redirect } from "next/navigation";
import { getCurrentUser } from "@/lib/auth";
import { saveNote } from "@/lib/db";

export type FormState = { error?: string; title: string };

export async function createNote(prev: FormState, formData: FormData): Promise<FormState> {
  const user = await getCurrentUser();
  if (!user) throw new Error("Unauthorized");

  const title = String(formData.get("title") ?? "").trim();
  if (title === "") return { error: "Введите заголовок", title };

  const note = await saveNote({ title, ownerId: user.id });
  revalidatePath("/notes");
  redirect(`/notes/${note.id}`);
}
```

Проверка пользователя стоит первой, а `redirect` - последней. Тип возвращаемого значения `Promise<FormState>` остаётся верным: `redirect` имеет тип `never`.

**Пример 2. Клиентская форма.**

```tsx
// app/notes/NoteForm.tsx
"use client";

import { useActionState } from "react";
import { createNote, type FormState } from "./actions";

const initialState: FormState = { title: "" };

export function NoteForm() {
  const [state, formAction, pending] = useActionState(createNote, initialState);
  return (
    <form action={formAction}>
      <input name="title" defaultValue={state.title} />
      {state.error && <p role="alert">{state.error}</p>}
      <button type="submit" disabled={pending}>
        {pending ? "Сохранение..." : "Сохранить"}
      </button>
    </form>
  );
}
```

`role="alert"` объявляет сообщение об ошибке вспомогательным технологиям. `disabled={pending}` защищает от двойной отправки.

**Пример 3. Дополнительный аргумент через `bind`.** Если действию нужен идентификатор, его передают привязкой, а не скрытым полем.

```tsx
// app/notes/[id]/edit/page.tsx
import { updateNote } from "../../actions";

export default async function EditPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = await params;
  const updateWithId = updateNote.bind(null, id);
  return (
    <form action={updateWithId}>
      <input name="title" />
      <button type="submit">Сохранить</button>
    </form>
  );
}
```

Действие объявлено как `updateNote(id: string, formData: FormData)`. Но и привязанный идентификатор приходит от клиента, поэтому действие обязано проверить, что пользователь вправе менять эту запись.

**Пример 4. Проверка владельца в действии удаления.**

```ts
"use server";

export async function deleteNote(id: string) {
  const user = await getCurrentUser();
  if (!user) return { ok: false, error: "Нужно войти" };
  const note = await findNote(id);
  if (!note || note.ownerId !== user.id) return { ok: false, error: "Нет доступа" };
  await removeNote(id);
  revalidatePath("/notes");
  return { ok: true };
}
```

Проверка выполняется при каждом вызове, даже если кнопка «Удалить» показывается только владельцу: скрытая кнопка не защищает функцию, у которой есть адрес.

<!-- section: mistakes -->
## Типичные ошибки

* **Доверие к странице вместо проверки в действии.** Проверка прав «форму видят только администраторы» не защищает действие: запрос POST можно отправить без формы. Аутентификацию и авторизацию проверяют внутри каждого действия.
* **Доверие к скрытым полям.** `<input type="hidden" name="userId" value="42">` лежит в HTML, и клиент вправе изменить его. Кто пользователь, определяют по сессии на сервере; клиент передаёт лишь идентификатор объекта, а права на него проверяются заново.
* **Нет серверной проверки.** Атрибут `required` и проверка в браузере улучшают удобство, но данные из `FormData` надо проверять в действии: тип, длину, диапазон.
* **`redirect` внутри `try`.** Функция бросает исключение; `catch` его перехватит, и перенаправления не произойдёт. Вызывайте `redirect` вне `try` и последним.
* **Код после `redirect`.** Он не выполняется. Обновление кэша (`revalidatePath`) поставьте раньше.
* **Директива не первой.** `"use server"` после импортов или после другого оператора не действует как директива. Для файла и для тела функции она должна идти первым оператором.
* **Объявление действия в клиентском компоненте.** Там его объявить нельзя: вынесите в файл с директивой и импортируйте.
* **Пустая форма после ошибки.** React 19 очищает неуправляемые поля после выполнения действия. Верните введённые значения в состоянии и подставьте в `defaultValue`.
* **Возврат лишнего.** Результат действия сериализуется и уходит клиенту. Возвращайте то, что нужно интерфейсу, а не запись базы целиком.

<!-- section: summary -->
## Итоги

* Серверное действие - асинхронная функция с директивой `"use server"` (в начале файла или первой строкой тела), вызываемая клиентом запросом POST.
* `<form action={fn}>` передаёт функции `FormData` с полями по атрибуту `name`; форма работает и до загрузки JavaScript.
* `useActionState(action, initialState)` возвращает `[state, formAction, pending]`; действие принимает `(prevState, formData)`, ошибки проверки возвращает как состояние.
* Введённые значения возвращают в состоянии и подставляют через `defaultValue`, потому что React 19 очищает поля после действия.
* После изменения вызывают `revalidatePath` (или теги), затем `redirect`: он бросает исключение, поэтому стоит последним и вне `try`.
* Каждое действие - публичная точка входа: проверка аутентификации, авторизации и входных данных выполняется внутри него, скрытым полям доверять нельзя.

<!-- section: programmer -->
## Если вы знаете другой веб-фреймворк

* Серверное действие заменяет пару «маршрут POST + `fetch` на клиенте»: адрес и сериализацию придумывает фреймворк, а вы пишете функцию. Разница с контроллером: у функции нет собственного URL в вашем коде, но запрос к ней отправить можно, поэтому защита нужна та же, что у обычной конечной точки (аутентификация, авторизация, CSRF).
* От CSRF Next.js защищает сравнением заголовков `Origin` и `Host` и допускает только POST; правила для прокси задаёт `serverActions.allowedOrigins`.
* Привычному `req.body` соответствует `FormData`, а `res.redirect` - вызов `redirect()`, который реализован через исключение.
* В Pages Router аналога нет: там формы отправляли через API-маршруты (`pages/api`). Урок относится только к App Router.
