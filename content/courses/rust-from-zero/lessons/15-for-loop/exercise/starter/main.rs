fn main() {
    let text = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut count = 0;
    let mut total = 0;
    let mut biggest = 0;

    // TODO: переберите строки text.lines(): пропустите пустые, остальные разберите
    // как числа, обновите count, total, biggest и напечатайте "<номер>. <цена>".
    let _ = (&text, &mut biggest);
    let _ = (&mut count, &mut total);

    println!("Позиций: {count}");
    println!("Итого: {total}");
    if count == 0 {
        println!("Самая дорогая: нет");
    } else {
        println!("Самая дорогая: {biggest}");
    }
}
