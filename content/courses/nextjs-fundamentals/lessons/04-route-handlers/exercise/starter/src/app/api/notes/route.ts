import { NextRequest, NextResponse } from "next/server";
import { createNote, listNotes } from "../../../lib/store";

export async function GET(request: NextRequest) {
  // TODO: прочитайте q и limit из request.nextUrl.searchParams, отфильтруйте listNotes()
  // и верните { notes, total }. Неверный limit - ответ 400 с { error }.
  return NextResponse.json({ error: "Не реализовано" }, { status: 501 });
}

export async function POST(request: NextRequest) {
  // TODO: проверьте Content-Type (415), разберите JSON (400), проверьте title и text (400),
  // создайте заметку через createNote и верните 201 с заголовком Location.
  return NextResponse.json({ error: "Не реализовано" }, { status: 501 });
}
