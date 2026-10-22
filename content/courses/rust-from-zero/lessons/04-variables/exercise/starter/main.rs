fn main() {
    let shop = "Булочная";
    let item = "Хлеб"; // TODO: замените на "Булка"
    let price = 40; // TODO: цена должна быть 45
    let count = 2; // TODO: эта переменная будет меняться - добавьте mut
    println!("Магазин: {shop}");
    println!("Товар: {item}, {price} руб.");
    println!("В корзине: {count}");
    println!("Добавили ещё одну булку");
    // TODO: запишите в count число 3 (присваивание без let)
    println!("В корзине: {count}");
}
