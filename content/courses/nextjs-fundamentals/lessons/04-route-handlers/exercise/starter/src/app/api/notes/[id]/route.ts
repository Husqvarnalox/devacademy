import { NextRequest, NextResponse } from "next/server";
import { deleteNote, getNote, updateNote } from "../../../../lib/store";

type Context = { params: Promise<{ id: string }> };

export async function GET(_request: NextRequest, { params }: Context) {
  // TODO: дождитесь params, найдите заметку; 200 с заметкой или 404 с { error }.
  return NextResponse.json({ error: "Не реализовано" }, { status: 501 });
}

export async function PATCH(request: NextRequest, { params }: Context) {
  // TODO: 404 для неизвестного id, затем 415, затем разбор JSON и проверка полей (400), затем updateNote.
  return NextResponse.json({ error: "Не реализовано" }, { status: 501 });
}

export async function DELETE(_request: NextRequest, { params }: Context) {
  // TODO: удалите заметку через deleteNote; 204 без тела или 404 с { error }.
  return NextResponse.json({ error: "Не реализовано" }, { status: 501 });
}
