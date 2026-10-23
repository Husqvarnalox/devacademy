fn main() {
    let mut title = String::from("Овсяная каша");
    title.push_str(" с мёдом");

    println!("Блюдо: {title}");
    println!("Заглавными: {}", title.to_uppercase());
    println!("Букв: {}", title.chars().count());
    println!("Байтов: {}", title.len());
}
