"""Разбор локальных HTML-фикстур и проверка robots.txt средствами стандартной библиотеки."""

import urllib.robotparser
from html.parser import HTMLParser
from urllib.parse import urldefrag, urljoin, urlsplit


def _clean(text):
    return " ".join(text.split())


class _LinkParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.hrefs = []

    def handle_starttag(self, tag, attrs):
        if tag == "a":
            for name, value in attrs:
                if name == "href" and value:
                    self.hrefs.append(value.strip())


def extract_links(html, base_url):
    parser = _LinkParser()
    parser.feed(html)
    parser.close()
    seen = set()
    links = []
    for href in parser.hrefs:
        if not href or href.startswith("#"):
            continue
        url, _ = urldefrag(urljoin(base_url, href))
        if urlsplit(url).scheme not in ("http", "https") or url in seen:
            continue
        seen.add(url)
        links.append(url)
    return links


class _TableParser(HTMLParser):
    def __init__(self):
        super().__init__(convert_charrefs=True)
        self.tables = []
        self._depth = 0
        self._table = None
        self._row = None
        self._cell = None
        self._skip = 0

    def _end_cell(self):
        if self._cell is not None:
            self._row.append(_clean("".join(self._cell)))
            self._cell = None

    def _end_row(self):
        self._end_cell()
        if self._row:
            self._table.append(self._row)
        self._row = None

    def _end_table(self):
        self._end_row()
        self.tables.append(self._table)
        self._table = None

    def handle_starttag(self, tag, attrs):
        if tag in ("script", "style"):
            self._skip += 1
        elif tag == "table":
            self._depth += 1
            if self._depth == 1:
                self._table = []
        elif self._depth == 1:
            if tag == "tr":
                self._end_row()
                self._row = []
            elif tag in ("td", "th"):
                self._end_cell()
                if self._row is None:
                    self._row = []
                self._cell = []
            elif tag == "br" and self._cell is not None:
                self._cell.append(" ")

    def handle_endtag(self, tag):
        if tag in ("script", "style"):
            self._skip = max(0, self._skip - 1)
        elif tag == "table":
            if self._depth == 1:
                self._end_table()
            self._depth = max(0, self._depth - 1)
        elif self._depth == 1:
            if tag in ("td", "th"):
                self._end_cell()
            elif tag == "tr":
                self._end_row()

    def handle_data(self, data):
        if self._depth == 1 and self._cell is not None and not self._skip:
            self._cell.append(data)

    def finish(self):
        self.close()
        if self._depth > 0 and self._table is not None:
            self._end_table()
            self._depth = 0


def extract_tables(html):
    parser = _TableParser()
    parser.feed(html)
    parser.finish()
    return parser.tables


def table_to_records(table):
    if not table:
        raise ValueError("пустая таблица")
    header = table[0]
    if any(not h for h in header) or len(set(header)) != len(header):
        raise ValueError(f"некорректный заголовок: {header!r}")
    records = []
    for number, row in enumerate(table[1:], 2):
        if len(row) > len(header):
            raise ValueError(f"строка {number}: ячеек больше, чем в заголовке")
        records.append(dict(zip(header, row + [""] * (len(header) - len(row)))))
    return records


def allowed_urls(robots_text, user_agent, urls):
    parser = urllib.robotparser.RobotFileParser()
    parser.parse(robots_text.splitlines())
    allowed = [url for url in urls if parser.can_fetch(user_agent, url)]
    return allowed, parser.crawl_delay(user_agent)


def politeness_interval(crawl_delay, minimum=1.0):
    if crawl_delay is None:
        return float(minimum)
    return float(max(crawl_delay, minimum))
