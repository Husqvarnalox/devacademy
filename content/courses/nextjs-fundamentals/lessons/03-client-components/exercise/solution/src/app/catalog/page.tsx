import Collapsible from "../../components/Collapsible";
import FavoriteButton from "../../components/FavoriteButton";
import NavLink from "../../components/NavLink";
import SearchBox from "../../components/SearchBox";
import { getCatalog } from "../../lib/catalog";

export default async function CatalogPage() {
  const items = await getCatalog();

  return (
    <main>
      <h1>Каталог</h1>
      <nav aria-label="Разделы">
        <NavLink href="/catalog">Все позиции</NavLink> <NavLink href="/catalog/new">Новинки</NavLink>
      </nav>
      <SearchBox />
      <Collapsible title="О каталоге">
        <p>Позиций в каталоге: {items.length}</p>
      </Collapsible>
      <ul>
        {items.map((item) => (
          <li key={item.id}>
            <span>{item.title}</span> <FavoriteButton title={item.title} />
          </li>
        ))}
      </ul>
    </main>
  );
}
