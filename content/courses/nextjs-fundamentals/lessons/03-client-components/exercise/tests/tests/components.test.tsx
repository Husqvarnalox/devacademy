import { readFileSync } from "node:fs";
import { join } from "node:path";
import { describe, it, expect, vi, beforeEach } from "vitest";
import { render, screen, fireEvent, within } from "@testing-library/react";

// Хуки next/navigation работают только внутри приложения Next.js, поэтому в тестах их заменяют управляемыми макетами.
const nav = vi.hoisted(() => ({ push: vi.fn(), pathname: "/catalog", search: "" }));
vi.mock("next/navigation", () => ({
  useRouter: () => ({ push: nav.push }),
  usePathname: () => nav.pathname,
  useSearchParams: () => new URLSearchParams(nav.search),
}));

import FavoriteButton from "../src/components/FavoriteButton";
import SearchBox from "../src/components/SearchBox";
import NavLink from "../src/components/NavLink";
import Collapsible from "../src/components/Collapsible";
import CatalogPage from "../src/app/catalog/page";

beforeEach(() => {
  nav.push.mockClear();
  nav.pathname = "/catalog";
  nav.search = "";
});

function source(file: string): string {
  return readFileSync(join(import.meta.dirname, "..", "src", file), "utf8");
}

// Убирает ведущие пробелы и комментарии: директива должна быть первым оператором модуля.
function withoutLeadingTrivia(code: string): string {
  let rest = code;
  for (;;) {
    const next = rest.replace(/^\s+/, "").replace(/^\/\/[^\n]*/, "").replace(/^\/\*[\s\S]*?\*\//, "");
    if (next === rest) return rest;
    rest = next;
  }
}

describe("FavoriteButton", () => {
  it("по клику переключает aria-pressed и подпись", () => {
    render(<FavoriteButton title="Чайник" />);
    const button = screen.getByRole("button", { name: "Чайник: В избранное" });
    expect(button.getAttribute("aria-pressed")).toBe("false");
    expect(button.textContent).toBe("В избранное");

    fireEvent.click(button);
    expect(button.getAttribute("aria-pressed")).toBe("true");
    expect(button.textContent).toBe("В избранном");
    expect(button.getAttribute("aria-label")).toBe("Чайник: В избранном");
  });

  it("повторный клик возвращает исходное состояние", () => {
    render(<FavoriteButton title="Блокнот" />);
    const button = screen.getByRole("button");
    fireEvent.click(button);
    fireEvent.click(button);
    expect(button.getAttribute("aria-pressed")).toBe("false");
    expect(button.textContent).toBe("В избранное");
  });

  it("учитывает начальное значение initial", () => {
    render(<FavoriteButton title="Кружка" initial />);
    expect(screen.getByRole("button", { name: "Кружка: В избранном" }).getAttribute("aria-pressed")).toBe("true");
  });

  it("состояние разных кнопок независимо", () => {
    render(
      <>
        <FavoriteButton title="Чайник" />
        <FavoriteButton title="Кружка" />
      </>,
    );
    fireEvent.click(screen.getByRole("button", { name: "Чайник: В избранное" }));
    expect(screen.getByRole("button", { name: "Кружка: В избранное" }).getAttribute("aria-pressed")).toBe("false");
  });
});

describe("SearchBox", () => {
  it("отправка ведёт на /search?q= с закодированным запросом", () => {
    render(<SearchBox />);
    fireEvent.change(screen.getByLabelText("Поиск"), { target: { value: "чай и кофе" } });
    fireEvent.click(screen.getByRole("button", { name: "Найти" }));
    expect(nav.push).toHaveBeenCalledTimes(1);
    expect(nav.push).toHaveBeenCalledWith("/search?q=" + encodeURIComponent("чай и кофе"));
  });

  it("подставляет начальное значение из параметра q", () => {
    nav.search = "q=" + encodeURIComponent("зелёный чай");
    render(<SearchBox />);
    expect((screen.getByLabelText("Поиск") as HTMLInputElement).value).toBe("зелёный чай");
  });

  it("без параметра q поле пустое", () => {
    render(<SearchBox />);
    expect((screen.getByLabelText("Поиск") as HTMLInputElement).value).toBe("");
  });

  it("обрезает пробелы по краям запроса", () => {
    render(<SearchBox />);
    fireEvent.change(screen.getByLabelText("Поиск"), { target: { value: "  чайник  " } });
    fireEvent.click(screen.getByRole("button", { name: "Найти" }));
    expect(nav.push).toHaveBeenCalledWith("/search?q=" + encodeURIComponent("чайник"));
  });

  it("запрос из пробелов ведёт на /search без параметра", () => {
    render(<SearchBox />);
    fireEvent.change(screen.getByLabelText("Поиск"), { target: { value: "   " } });
    fireEvent.click(screen.getByRole("button", { name: "Найти" }));
    expect(nav.push).toHaveBeenCalledWith("/search");
  });

  it("кодирует спецсимволы запроса", () => {
    render(<SearchBox />);
    fireEvent.change(screen.getByLabelText("Поиск"), { target: { value: "a&b=c" } });
    fireEvent.click(screen.getByRole("button", { name: "Найти" }));
    expect(nav.push).toHaveBeenCalledWith("/search?q=a%26b%3Dc");
  });

  it("отменяет стандартную отправку формы", () => {
    render(<SearchBox />);
    const notCancelled = fireEvent.submit(screen.getByRole("search"));
    expect(notCancelled).toBe(false);
  });
});

describe("NavLink", () => {
  it("помечает ссылку на текущий адрес атрибутом aria-current", () => {
    render(<NavLink href="/catalog">Все позиции</NavLink>);
    expect(screen.getByRole("link", { name: "Все позиции" }).getAttribute("aria-current")).toBe("page");
  });

  it("не помечает ссылку на другой адрес", () => {
    render(<NavLink href="/catalog/new">Новинки</NavLink>);
    expect(screen.getByRole("link", { name: "Новинки" }).hasAttribute("aria-current")).toBe(false);
  });

  it("ведёт на указанный href", () => {
    render(<NavLink href="/about">О нас</NavLink>);
    expect(screen.getByRole("link", { name: "О нас" }).getAttribute("href")).toBe("/about");
  });

  it("сравнивает адрес целиком, а не по началу", () => {
    nav.pathname = "/catalog/new";
    render(<NavLink href="/catalog">Все позиции</NavLink>);
    expect(screen.getByRole("link").hasAttribute("aria-current")).toBe(false);
  });
});

describe("Collapsible", () => {
  it("по умолчанию скрывает содержимое", () => {
    render(
      <Collapsible title="Подробнее">
        <p>Скрытый текст</p>
      </Collapsible>,
    );
    expect(screen.queryByText("Скрытый текст")).toBeNull();
    expect(screen.getByRole("button", { name: "Подробнее" }).getAttribute("aria-expanded")).toBe("false");
  });

  it("клик по заголовку показывает и скрывает содержимое", () => {
    render(
      <Collapsible title="Подробнее">
        <p>Скрытый текст</p>
      </Collapsible>,
    );
    const toggle = screen.getByRole("button", { name: "Подробнее" });
    fireEvent.click(toggle);
    expect(screen.getByText("Скрытый текст")).toBeTruthy();
    expect(toggle.getAttribute("aria-expanded")).toBe("true");
    fireEvent.click(toggle);
    expect(screen.queryByText("Скрытый текст")).toBeNull();
    expect(toggle.getAttribute("aria-expanded")).toBe("false");
  });

  it("defaultOpen открывает содержимое сразу", () => {
    render(
      <Collapsible title="Подробнее" defaultOpen>
        <p>Видимый текст</p>
      </Collapsible>,
    );
    expect(screen.getByText("Видимый текст")).toBeTruthy();
    expect(screen.getByRole("button", { name: "Подробнее" }).getAttribute("aria-expanded")).toBe("true");
  });
});

describe("CatalogPage", () => {
  it("показывает заголовок и все позиции каталога", async () => {
    render(await CatalogPage());
    expect(screen.getByRole("heading", { level: 1, name: "Каталог" })).toBeTruthy();
    const items = screen.getAllByRole("listitem");
    expect(items.map((item) => item.querySelector("span")?.textContent)).toEqual(["Чайник", "Блокнот", "Кружка"]);
  });

  it("собирает интерактивные части из клиентских компонентов", async () => {
    render(await CatalogPage());
    expect(screen.getByRole("search")).toBeTruthy();
    const navigation = screen.getByRole("navigation", { name: "Разделы" });
    expect(within(navigation).getByRole("link", { name: "Все позиции" }).getAttribute("href")).toBe("/catalog");
    expect(within(navigation).getByRole("link", { name: "Все позиции" }).getAttribute("aria-current")).toBe("page");
    expect(within(navigation).getByRole("link", { name: "Новинки" }).getAttribute("href")).toBe("/catalog/new");
    fireEvent.click(screen.getByRole("button", { name: "Блокнот: В избранное" }));
    expect(screen.getByRole("button", { name: "Блокнот: В избранном" })).toBeTruthy();
    expect(screen.getByRole("button", { name: "Чайник: В избранное" })).toBeTruthy();
  });

  it("передаёт серверное содержимое внутрь сворачиваемого блока", async () => {
    render(await CatalogPage());
    expect(screen.queryByText("Позиций в каталоге: 3")).toBeNull();
    fireEvent.click(screen.getByRole("button", { name: "О каталоге" }));
    expect(screen.getByText("Позиций в каталоге: 3")).toBeTruthy();
  });
});

describe("Граница use client", () => {
  it("интерактивные компоненты начинаются с директивы use client", () => {
    for (const file of ["FavoriteButton", "SearchBox", "NavLink", "Collapsible"]) {
      const code = withoutLeadingTrivia(source(`components/${file}.tsx`));
      expect(code, file).toMatch(/^["']use client["']/);
    }
  });

  it("страница каталога остаётся серверным компонентом", () => {
    const code = source("app/catalog/page.tsx");
    expect(code).not.toMatch(/["']use client["']/);
    expect(code).not.toMatch(/\buse(State|Effect|Router|Pathname|SearchParams)\b/);
  });
});
