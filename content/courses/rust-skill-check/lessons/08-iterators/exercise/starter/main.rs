use std::collections::BTreeMap;

// Квадраты чётных чисел в исходном порядке.
fn evens_squared(v: &[i32]) -> Vec<i32> {
    // TODO: filter + map + collect
    Vec::new()
}

// Накопленные суммы: [1, 2, 3] -> [1, 3, 6].
fn running_sum(v: &[i32]) -> Vec<i32> {
    // TODO: scan или цикл с накоплением
    Vec::new()
}

// Самое длинное слово (по len()); при равенстве - первое; None, если слов нет.
fn longest_word(text: &str) -> Option<&str> {
    // TODO: split_whitespace + fold или max_by_key
    None
}

// Частоты слов без учёта регистра.
fn word_freq(text: &str) -> BTreeMap<String, usize> {
    // TODO: entry(...).or_insert(0)
    BTreeMap::new()
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    for line in input.lines() {
        let (command, rest) = match line.split_once(' ') {
            Some((c, r)) => (c, r),
            None => (line, ""),
        };
        match command {
            "sq" | "run" => {
                let numbers: Vec<i32> = rest.split_whitespace().map(|w| w.parse().unwrap()).collect();
                let result = if command == "sq" {
                    evens_squared(&numbers)
                } else {
                    running_sum(&numbers)
                };
                println!("{:?}", result);
            }
            "longest" => match longest_word(rest) {
                Some(word) => println!("{}", word),
                None => println!("none"),
            },
            "freq" => {
                let freq = word_freq(rest);
                let parts: Vec<String> = freq.iter().map(|(w, n)| format!("{}={}", w, n)).collect();
                println!("{}", parts.join(" "));
            }
            _ => {}
        }
    }
}
