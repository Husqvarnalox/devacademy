fn main() {
    let product = "Чай";
    // TODO: вес в килограммах - 0.25
    let weight: f64 = 0.0;
    // TODO: товар есть в наличии - true
    let in_stock: bool = false;
    // TODO: категория - символ 'B' в одинарных кавычках
    let category: char = 'A';
    // TODO: продано 8100000000 пакетиков - это число не помещается в i32,
    // поэтому смените тип на i64 и впишите значение
    let sold: i32 = 0;
    println!("Товар: {product}");
    println!("Вес, кг: {weight}");
    println!("В наличии: {in_stock}");
    println!("Категория: {category}");
    println!("Продано пакетиков: {sold}");
}
