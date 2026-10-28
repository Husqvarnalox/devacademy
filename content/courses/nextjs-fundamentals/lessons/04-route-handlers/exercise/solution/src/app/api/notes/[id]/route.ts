import { NextRequest, NextResponse } from "next/server";
import { deleteNote, getNote, updateNote } from "../../../../lib/store";

type Context = { params: Promise<{ id: string }> };

const MAX_TITLE = 100;

export async function GET(_request: NextRequest, { params }: Context) {
  const { id } = await params;
  const note = getNote(id);
  if (!note) {
    return NextResponse.json({ error: "Заметка не найдена" }, { status: 404 });
  }
  return NextResponse.json(note);
}

export async function PATCH(request: NextRequest, { params }: Context) {
  const { id } = await params;
  if (!getNote(id)) {
    return NextResponse.json({ error: "Заметка не найдена" }, { status: 404 });
  }
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
  if (title === undefined && text === undefined) {
    return NextResponse.json({ error: "Нужно передать title или text" }, { status: 400 });
  }
  if (title !== undefined && (typeof title !== "string" || title.trim() === "" || title.trim().length > MAX_TITLE)) {
    return NextResponse.json({ error: "title: строка от 1 до 100 символов" }, { status: 400 });
  }
  if (text !== undefined && typeof text !== "string") {
    return NextResponse.json({ error: "text должен быть строкой" }, { status: 400 });
  }

  const updated = updateNote(id, {
    title: typeof title === "string" ? title.trim() : undefined,
    text: typeof text === "string" ? text : undefined,
  });
  return NextResponse.json(updated);
}

export async function DELETE(_request: NextRequest, { params }: Context) {
  const { id } = await params;
  if (!deleteNote(id)) {
    return NextResponse.json({ error: "Заметка не найдена" }, { status: 404 });
  }
  return new NextResponse(null, { status: 204 });
}
