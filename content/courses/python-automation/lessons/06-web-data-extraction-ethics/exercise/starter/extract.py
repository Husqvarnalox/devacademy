"""Разбор локальных HTML-фикстур и проверка robots.txt средствами стандартной библиотеки."""

import urllib.robotparser
from html.parser import HTMLParser
from urllib.parse import urldefrag, urljoin, urlsplit


def extract_links(html, base_url):
    """Абсолютные http/https-ссылки из <a href> в порядке появления, без повторов.
    * относительные ссылки разрешать через urljoin(base_url, href);
    * фрагмент (#...) отбрасывать; ссылки, состоящие только из фрагмента, и пустые href пропускать;
    * схемы, кроме http и https (mailto:, javascript:, tel:, ftp:), пропускать;
    * кривая разметка (незакрытые теги, атрибуты без кавычек, регистр тегов) не должна ломать разбор.
    Используйте html.parser.HTMLParser."""
    raise NotImplementedError


def extract_tables(html):
    """Все таблицы верхнего уровня: список таблиц, таблица - список строк, строка - список текстов ячеек
    (th и td одинаково). Текст ячейки: без тегов, пробельные символы (в т.ч. неразрывные) схлопнуты в один пробел,
    сущности (&amp;) декодированы, <br> даёт пробел, script/style игнорируются.
    Устойчивость: незакрытые <td>/<tr>/<table> (конец входа закрывает всё), ячейки вне <tr>, строки без ячеек
    пропускаются. Вложенные таблицы внутри ячейки игнорируются целиком (их текст в ячейку не попадает)."""
    raise NotImplementedError


def table_to_records(table):
    """Таблица (список строк) -> список словарей по первой строке-заголовку.
    Пустая таблица, пустые или повторяющиеся имена заголовков - ValueError.
    Строка с БОЛЬШИМ числом ячеек, чем в заголовке - ValueError (с номером строки, заголовок - 1);
    с меньшим - недостающие значения ''."""
    raise NotImplementedError


def allowed_urls(robots_text, user_agent, urls):
    """По тексту robots.txt (urllib.robotparser.RobotFileParser.parse) вернуть (разрешённые urls в исходном порядке,
    crawl_delay для user_agent или None)."""
    raise NotImplementedError


def politeness_interval(crawl_delay, minimum=1.0):
    """Минимальная пауза между запросами: собственный минимум, но не меньше Crawl-delay сайта. float."""
    raise NotImplementedError
