fn main() {
    // TODO 1: превратите title в String, который можно менять:
    // let mut title = String::from("Овсяная каша");
    let title = "Овсяная каша";
    // TODO 2: допишите к title текст " с мёдом" командой push_str

    println!("Блюдо: {title}");
    println!("Заглавными: {}", title.to_uppercase());
    println!("Букв: {}", title.chars().count());
    println!("Байтов: {}", title.len());
}
