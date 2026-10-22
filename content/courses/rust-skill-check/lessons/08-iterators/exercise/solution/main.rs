use std::collections::BTreeMap;

fn evens_squared(v: &[i32]) -> Vec<i32> {
    v.iter().filter(|n| *n % 2 == 0).map(|n| n * n).collect()
}

fn running_sum(v: &[i32]) -> Vec<i32> {
    v.iter()
        .scan(0, |acc, n| {
            *acc += n;
            Some(*acc)
        })
        .collect()
}

fn longest_word(text: &str) -> Option<&str> {
    text.split_whitespace()
        .fold(None, |best: Option<&str>, word| match best {
            Some(current) if current.len() >= word.len() => Some(current),
            _ => Some(word),
        })
}

fn word_freq(text: &str) -> BTreeMap<String, usize> {
    let mut map = BTreeMap::new();
    for word in text.split_whitespace() {
        *map.entry(word.to_lowercase()).or_insert(0) += 1;
    }
    map
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
