fn word_length(text: &str) -> usize {
    // TODO: верните число символов (не байт) в тексте
    0
}

fn add_signature(text: &mut String) {
    // TODO: допишите в конец " (проверено)"
}

fn double_in_place(value: &mut i32) {
    // TODO: удвойте число
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
