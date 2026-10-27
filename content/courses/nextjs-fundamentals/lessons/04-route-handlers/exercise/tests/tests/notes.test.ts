// @vitest-environment node
import { describe, it, expect, beforeEach } from "vitest";
import { NextRequest } from "next/server";
import { resetNotes } from "../src/lib/store";
import { GET, POST } from "../src/app/api/notes/route";
import { GET as GET_ONE, PATCH, DELETE } from "../src/app/api/notes/[id]/route";

const BASE = "http://localhost:3000";
const JSON_HEADERS = { "content-type": "application/json" };

beforeEach(() => {
  resetNotes();
});

function get(path: string): NextRequest {
  return new NextRequest(BASE + path);
}

function send(method: string, path: string, body: unknown, headers: Record<string, string> = JSON_HEADERS): NextRequest {
  return new NextRequest(BASE + path, {
    method,
    headers,
    body: typeof body === "string" ? body : JSON.stringify(body),
  });
}

function ctx(id: string) {
  return { params: Promise.resolve({ id }) };
}

async function ids(response: Response): Promise<string[]> {
  const data = (await response.json()) as { notes: { id: string }[] };
  return data.notes.map((note) => note.id);
}

describe("GET /api/notes", () => {
  it("возвращает все заметки и общее количество", async () => {
    const response = await GET(get("/api/notes"));
    expect(response.status).toBe(200);
    const data = (await response.json()) as { notes: { id: string; title: string }[]; total: number };
    expect(data.total).toBe(3);
    expect(data.notes.map((note) => note.id)).toEqual(["1", "2", "3"]);
    expect(data.notes[0]).toEqual({ id: "1", title: "Покупки", text: "Чай, хлеб, молоко" });
  });

  it("фильтрует по q без учёта регистра в заголовке и тексте", async () => {
    expect(await ids(await GET(get("/api/notes?q=" + encodeURIComponent("чай"))))).toEqual(["1", "3"]);
    expect(await ids(await GET(get("/api/notes?q=" + encodeURIComponent("ЧАЙ"))))).toEqual(["1", "3"]);
    expect(await ids(await GET(get("/api/notes?q=" + encodeURIComponent("МАРШРУТА"))))).toEqual(["2"]);
  });

  it("пустой q или q из пробелов не фильтрует", async () => {
    expect(await ids(await GET(get("/api/notes?q=")))).toEqual(["1", "2", "3"]);
    expect(await ids(await GET(get("/api/notes?q=%20%20")))).toEqual(["1", "2", "3"]);
  });

  it("при отсутствии совпадений возвращает пустой список и total 0", async () => {
    const response = await GET(get("/api/notes?q=" + encodeURIComponent("нет такого")));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ notes: [], total: 0 });
  });

  it("limit ограничивает список, а total считает все найденные", async () => {
    const response = await GET(get("/api/notes?limit=2"));
    const data = (await response.json()) as { notes: unknown[]; total: number };
    expect(data.notes).toHaveLength(2);
    expect(data.total).toBe(3);
  });

  it("limit применяется после фильтра по q", async () => {
    const response = await GET(get("/api/notes?limit=1&q=" + encodeURIComponent("чай")));
    const data = (await response.json()) as { notes: { id: string }[]; total: number };
    expect(data.notes.map((note) => note.id)).toEqual(["1"]);
    expect(data.total).toBe(2);
  });

  it("граничные значения limit 1 и 50 допустимы", async () => {
    expect((await GET(get("/api/notes?limit=1"))).status).toBe(200);
    expect((await GET(get("/api/notes?limit=50"))).status).toBe(200);
  });

  it("недопустимый limit даёт 400 и сообщение об ошибке", async () => {
    for (const value of ["0", "51", "-1", "abc", "1.5", ""]) {
      const response = await GET(get(`/api/notes?limit=${value}`));
      expect(response.status, `limit=${value}`).toBe(400);
      const data = (await response.json()) as { error: unknown };
      expect(typeof data.error).toBe("string");
      expect((data.error as string).length).toBeGreaterThan(0);
    }
  });
});

describe("POST /api/notes", () => {
  it("создаёт заметку: 201, тело и заголовок Location", async () => {
    const response = await POST(send("POST", "/api/notes", { title: "Новая заметка", text: "Текст" }));
    expect(response.status).toBe(201);
    expect(response.headers.get("Location")).toBe("/api/notes/4");
    expect(await response.json()).toEqual({ id: "4", title: "Новая заметка", text: "Текст" });
  });

  it("созданная заметка появляется в списке", async () => {
    await POST(send("POST", "/api/notes", { title: "Видимая" }));
    const response = await GET(get("/api/notes"));
    expect(await ids(response)).toEqual(["1", "2", "3", "4"]);
  });

  it("обрезает пробелы в title, а text по умолчанию пустой", async () => {
    const response = await POST(send("POST", "/api/notes", { title: "  Заголовок  " }));
    expect(response.status).toBe(201);
    expect(await response.json()).toEqual({ id: "4", title: "Заголовок", text: "" });
  });

  it("без JSON в Content-Type отвечает 415", async () => {
    const response = await POST(send("POST", "/api/notes", '{"title":"x"}', { "content-type": "text/plain" }));
    expect(response.status).toBe(415);
    const data = (await response.json()) as { error: unknown };
    expect(typeof data.error).toBe("string");
  });

  it("запрос без Content-Type отвечает 415", async () => {
    const response = await POST(new NextRequest(BASE + "/api/notes", { method: "POST", body: '{"title":"x"}' }));
    expect(response.status).toBe(415);
  });

  it("некорректный JSON даёт 400", async () => {
    const response = await POST(send("POST", "/api/notes", "{не json"));
    expect(response.status).toBe(400);
  });

  it("тело, не являющееся объектом, даёт 400", async () => {
    expect((await POST(send("POST", "/api/notes", "[]"))).status).toBe(400);
    expect((await POST(send("POST", "/api/notes", "null"))).status).toBe(400);
    expect((await POST(send("POST", "/api/notes", '"строка"'))).status).toBe(400);
  });

  it("пустой, отсутствующий или нестроковый title даёт 400", async () => {
    for (const body of [{}, { title: "" }, { title: "   " }, { title: 5 }, { title: null }]) {
      const response = await POST(send("POST", "/api/notes", body));
      expect(response.status, JSON.stringify(body)).toBe(400);
    }
  });

  it("title до 100 символов допустим, 101 символ даёт 400", async () => {
    const ok = await POST(send("POST", "/api/notes", { title: "я".repeat(100) }));
    expect(ok.status).toBe(201);
    const tooLong = await POST(send("POST", "/api/notes", { title: "я".repeat(101) }));
    expect(tooLong.status).toBe(400);
  });

  it("длину title считают после обрезки пробелов", async () => {
    const response = await POST(send("POST", "/api/notes", { title: " " + "я".repeat(100) + " " }));
    expect(response.status).toBe(201);
  });

  it("нестроковый text даёт 400 и заметка не создаётся", async () => {
    const response = await POST(send("POST", "/api/notes", { title: "Заголовок", text: 10 }));
    expect(response.status).toBe(400);
    expect(await ids(await GET(get("/api/notes")))).toEqual(["1", "2", "3"]);
  });
});

describe("GET /api/notes/[id]", () => {
  it("возвращает заметку по id", async () => {
    const response = await GET_ONE(get("/api/notes/2"), ctx("2"));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ id: "2", title: "Идеи для урока", text: "Показать обработчик маршрута" });
  });

  it("для неизвестного id отвечает 404 с сообщением", async () => {
    const response = await GET_ONE(get("/api/notes/99"), ctx("99"));
    expect(response.status).toBe(404);
    const data = (await response.json()) as { error: unknown };
    expect(typeof data.error).toBe("string");
  });
});

describe("PATCH /api/notes/[id]", () => {
  it("обновляет только переданное поле", async () => {
    const response = await PATCH(send("PATCH", "/api/notes/1", { title: "  Новые покупки " }), ctx("1"));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ id: "1", title: "Новые покупки", text: "Чай, хлеб, молоко" });
  });

  it("обновляет text, не меняя title", async () => {
    const response = await PATCH(send("PATCH", "/api/notes/1", { text: "" }), ctx("1"));
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ id: "1", title: "Покупки", text: "" });
  });

  it("изменение сохраняется в хранилище", async () => {
    await PATCH(send("PATCH", "/api/notes/3", { text: "Только пуэр" }), ctx("3"));
    const response = await GET_ONE(get("/api/notes/3"), ctx("3"));
    expect(await response.json()).toEqual({ id: "3", title: "Чайный список", text: "Только пуэр" });
  });

  it("для неизвестного id отвечает 404 раньше проверки тела", async () => {
    const response = await PATCH(send("PATCH", "/api/notes/99", "не json", { "content-type": "text/plain" }), ctx("99"));
    expect(response.status).toBe(404);
  });

  it("без JSON в Content-Type отвечает 415", async () => {
    const response = await PATCH(send("PATCH", "/api/notes/1", '{"text":"x"}', { "content-type": "text/plain" }), ctx("1"));
    expect(response.status).toBe(415);
  });

  it("пустой объект и некорректный JSON дают 400", async () => {
    expect((await PATCH(send("PATCH", "/api/notes/1", {}), ctx("1"))).status).toBe(400);
    expect((await PATCH(send("PATCH", "/api/notes/1", "{не json"), ctx("1"))).status).toBe(400);
    expect((await PATCH(send("PATCH", "/api/notes/1", "[]"), ctx("1"))).status).toBe(400);
  });

  it("недопустимые значения полей дают 400 и ничего не меняют", async () => {
    for (const body of [{ title: "" }, { title: "   " }, { title: 1 }, { title: "я".repeat(101) }, { text: 5 }]) {
      const response = await PATCH(send("PATCH", "/api/notes/1", body), ctx("1"));
      expect(response.status, JSON.stringify(body)).toBe(400);
    }
    const after = await GET_ONE(get("/api/notes/1"), ctx("1"));
    expect(await after.json()).toEqual({ id: "1", title: "Покупки", text: "Чай, хлеб, молоко" });
  });
});

describe("DELETE /api/notes/[id]", () => {
  it("удаляет заметку и отвечает 204 без тела", async () => {
    const response = await DELETE(new NextRequest(BASE + "/api/notes/2", { method: "DELETE" }), ctx("2"));
    expect(response.status).toBe(204);
    expect(await response.text()).toBe("");
    expect((await GET_ONE(get("/api/notes/2"), ctx("2"))).status).toBe(404);
    expect(await ids(await GET(get("/api/notes")))).toEqual(["1", "3"]);
  });

  it("для неизвестного id отвечает 404", async () => {
    const response = await DELETE(new NextRequest(BASE + "/api/notes/99", { method: "DELETE" }), ctx("99"));
    expect(response.status).toBe(404);
    const data = (await response.json()) as { error: unknown };
    expect(typeof data.error).toBe("string");
  });

  it("повторное удаление даёт 404", async () => {
    const request = () => new NextRequest(BASE + "/api/notes/1", { method: "DELETE" });
    expect((await DELETE(request(), ctx("1"))).status).toBe(204);
    expect((await DELETE(request(), ctx("1"))).status).toBe(404);
  });
});
