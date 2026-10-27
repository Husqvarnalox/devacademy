import { NextRequest, NextResponse } from "next/server";
import { createNote, listNotes } from "../../../lib/store";

const MAX_TITLE = 100;

export async function GET(request: NextRequest) {
  const { searchParams } = request.nextUrl;

  let limit: number | undefined;
  const rawLimit = searchParams.get("limit");
  if (rawLimit !== null) {
    limit = /^\d+$/.test(rawLimit) ? Number(rawLimit) : NaN;
    if (!(limit >= 1 && limit <= 50)) {
      return NextResponse.json({ error: "limit должен быть целым числом от 1 до 50" }, { status: 400 });
    }
  }

  const query = (searchParams.get("q") ?? "").trim().toLowerCase();
  const found = listNotes().filter(
    (note) => query === "" || note.title.toLowerCase().includes(query) || note.text.toLowerCase().includes(query),
  );

  return NextResponse.json({ notes: limit === undefined ? found : found.slice(0, limit), total: found.length });
}

export async function POST(request: NextRequest) {
  if (!request.headers.get("content-type")?.includes("application/json")) {
    return NextResponse.json({ error: "Ожидается Content-Type: application/json" }, { status: 415 });
  }

  let body: unknown;
  try {
    body = await request.json();
  } catch {
    return NextResponse.json({ error: "Тело запроса не является корректным JSON" }, { status: 400 });
  }
  if (typeof body !== "object" || body === null || Array.isArray(body)) {
    return NextResponse.json({ error: "Ожидается JSON-объект" }, { status: 400 });
  }

  const { title, text } = body as { title?: unknown; text?: unknown };
  if (typeof title !== "string" || title.trim() === "" || title.trim().length > MAX_TITLE) {
    return NextResponse.json({ error: "title: строка от 1 до 100 символов" }, { status: 400 });
  }
  if (text !== undefined && typeof text !== "string") {
    return NextResponse.json({ error: "text должен быть строкой" }, { status: 400 });
  }

  const note = createNote({ title: title.trim(), text: text ?? "" });
  return NextResponse.json(note, { status: 201, headers: { Location: `/api/notes/${note.id}` } });
}
