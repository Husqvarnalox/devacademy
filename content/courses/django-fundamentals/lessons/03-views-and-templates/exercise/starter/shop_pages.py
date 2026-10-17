"""Представления и шаблоны. Шаблоны хранятся в словаре (загрузчик locmem), файлов на диске нет.

Каркас не меняйте. Ваш код - в двух местах, помеченных «ВАШ КОД».
"""
# ---- шаблоны ----
# base.html дан; list.html, detail.html и about.html допишите.
TEMPLATES_SOURCES = {
    "base.html": (
        "<html><head><title>{% block title %}Shop{% endblock %}</title></head>"
        "<body><h1>{% block heading %}Shop{% endblock %}</h1>{% block content %}{% endblock %}</body></html>"
    ),
    # ВАШ КОД (1): шаблоны. Каждый наследует base.html ({% extends "base.html" %}).
    #
    # list.html:   title и heading = "Products"; если задан q - <p class="q">Search: {{ q }}</p>;
    #              затем <ul> с <li>{{ p.name }}: {{ p.price|floatformat:2 }}</li> для каждого p в products,
    #              а для пустого списка (блок {% empty %}) один <li>No products</li>.
    # detail.html: title и heading = имя товара; <p class="note"> с заметкой product.note (переводы строк -> <br>, без |safe);
    #              затем <span>In stock</span> или <span>Sold out</span> по product.in_stock.
    # about.html:  title = "About"; в блоке content <p>{{ site_name }}</p>.
    "list.html": "",
    "detail.html": "",
    "about.html": "",
}

# ---- каркас ----
import django
from django.conf import settings

if not settings.configured:
    settings.configure(
        DEBUG=False,
        SECRET_KEY="dev-only-secret-not-for-production",
        ROOT_URLCONF=__name__,
        ALLOWED_HOSTS=["testserver"],
        INSTALLED_APPS=[],
        MIDDLEWARE=[],
        TEMPLATES=[
            {
                "BACKEND": "django.template.backends.django.DjangoTemplates",
                "OPTIONS": {"loaders": [("django.template.loaders.locmem.Loader", TEMPLATES_SOURCES)]},
            }
        ],
    )
    django.setup()

from django.http import Http404
from django.shortcuts import render
from django.urls import path
from django.views import View
from django.views.generic import TemplateView

PRODUCTS = [
    {"id": 1, "name": "Pen", "price": 1.5, "note": "Blue ink", "in_stock": True},
    {"id": 2, "name": "Notebook", "price": 4, "note": "A5\n80 pages", "in_stock": False},
    {"id": 3, "name": "Pencil <HB>", "price": 0.5, "note": "<script>alert(1)</script>", "in_stock": True},
]


# ---- ВАШ КОД (2): представления и маршруты ----
def product_list(request):
    """Функциональное представление: список товаров; ?q=... - фильтр по подстроке имени без учёта регистра.
    Контекст шаблона list.html: products (список словарей) и q (строка поиска, пустая если не задана)."""
    raise NotImplementedError  # TODO: render(request, "list.html", {...})


class ProductDetail(View):
    """Классовое представление: GET /products/<pk>/ ; неизвестный pk -> 404 ; другие методы -> 405 (View делает это сам)."""

    def get(self, request, pk):
        raise NotImplementedError  # TODO: найти товар в PRODUCTS, иначе raise Http404; render "detail.html" с контекстом {"product": ...}


# TODO: маршруты products/ (product-list), products/<int:pk>/ (product-detail) и about/ (about);
# about - TemplateView.as_view(template_name="about.html", extra_context={"site_name": "Pen Shop"}).
urlpatterns = []


if __name__ == "__main__":
    from django.test import Client

    client = Client()
    print(client.get("/products/?q=pen").content.decode())
