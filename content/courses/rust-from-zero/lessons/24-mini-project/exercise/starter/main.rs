use std::collections::HashMap;

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();

    // TODO: пройдите по строкам input.lines(): trim, пропуск пустых,
    // разбор «имя;балл», накопление суммы и количества по каждому имени.
    // TODO: напечатайте отчёт: студенты по алфавиту, Записей, Лучший, Неверных строк.
    let table: HashMap<String, i32> = HashMap::new();
    println!("{}", table.len());
}
