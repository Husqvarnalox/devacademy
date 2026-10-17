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
    # ВАШ КОД (1): шаблоны
    "list.html": (
        '{% extends "base.html" %}'
        "{% block title %}Products{% endblock %}"
        "{% block heading %}Products{% endblock %}"
        "{% block content %}"
        '{% if q %}<p class="q">Search: {{ q }}</p>{% endif %}'
        "<ul>{% for p in products %}<li>{{ p.name }}: {{ p.price|floatformat:2 }}</li>{% empty %}<li>No products</li>{% endfor %}</ul>"
        "{% endblock %}"
    ),
    "detail.html": (
        '{% extends "base.html" %}'
        "{% block title %}{{ product.name }}{% endblock %}"
        "{% block heading %}{{ product.name }}{% endblock %}"
        "{% block content %}"
        '<p class="note">{{ product.note|linebreaksbr }}</p>'
        "{% if product.in_stock %}<span>In stock</span>{% else %}<span>Sold out</span>{% endif %}"
        "{% endblock %}"
    ),
    "about.html": (
        '{% extends "base.html" %}'
        "{% block title %}About{% endblock %}"
        "{% block content %}<p>{{ site_name }}</p>{% endblock %}"
    ),
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
    """Функциональное представление: список товаров; ?q=... - фильтр по подстроке имени без учёта регистра."""
    q = request.GET.get("q", "").strip()
    products = [p for p in PRODUCTS if q.lower() in p["name"].lower()] if q else list(PRODUCTS)
    return render(request, "list.html", {"products": products, "q": q})


class ProductDetail(View):
    """Классовое представление: GET /products/<pk>/ ; неизвестный pk -> 404 ; другие методы -> 405 (View делает это сам)."""

    def get(self, request, pk):
        for product in PRODUCTS:
            if product["id"] == pk:
                return render(request, "detail.html", {"product": product})
        raise Http404("no such product")


urlpatterns = [
    path("products/", product_list, name="product-list"),
    path("products/<int:pk>/", ProductDetail.as_view(), name="product-detail"),
    path("about/", TemplateView.as_view(template_name="about.html", extra_context={"site_name": "Pen Shop"}), name="about"),
]


if __name__ == "__main__":
    from django.test import Client

    client = Client()
    print(client.get("/products/?q=pen").content.decode())
    print(client.get("/products/3/").content.decode())
