fn main() {
    let shop = "Булочная";
    let item = "Булка";
    let price = 45;
    let mut count = 2;
    println!("Магазин: {shop}");
    println!("Товар: {item}, {price} руб.");
    println!("В корзине: {count}");
    println!("Добавили ещё одну булку");
    count = 3;
    println!("В корзине: {count}");
}
