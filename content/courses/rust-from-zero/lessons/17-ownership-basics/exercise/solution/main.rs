fn word_length(text: &str) -> usize {
    text.chars().count()
}

fn add_signature(text: &mut String) {
    text.push_str(" (проверено)");
}

fn double_in_place(value: &mut i32) {
    *value *= 2;
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = input.lines();
    let mut title = String::from(lines.next().unwrap().trim());
    let mut copies: i32 = lines.next().unwrap().trim().parse().unwrap();

    let original = title.clone();
    let length = word_length(&title);
    add_signature(&mut title);
    double_in_place(&mut copies);

    println!("Было: {original}");
    println!("Стало: {title}");
    println!("Символов в исходном названии: {length}");
    println!("Тираж: {copies}");
}
