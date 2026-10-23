use std::collections::HashMap;

fn last_index_of(values: &Vec<i32>, target: i32) -> i32 {
    let mut result = -1;
    for (i, value) in values.iter().enumerate() {
        if *value == target {
            result = i as i32;
        }
    }
    result
}

fn remove_negatives(values: &mut Vec<i32>) {
    values.retain(|x| *x >= 0);
}

fn count_words(words: &Vec<String>) -> HashMap<String, i32> {
    let mut counts = HashMap::new();
    for word in words {
        *counts.entry(word.clone()).or_insert(0) += 1;
    }
    counts
}

fn middle(text: &str) -> String {
    let letters: Vec<char> = text.chars().collect();
    let n = letters.len();
    if n == 0 {
        return String::new();
    }
    let half = n / 2;
    if n % 2 == 1 {
        letters[half].to_string()
    } else {
        letters[half - 1..=half].iter().collect()
    }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    for line in input.lines() {
        let mut parts = line.split_whitespace();
        let command = parts.next().unwrap_or("");
        match command {
            "last" => {
                let target: i32 = parts.next().unwrap().parse().unwrap();
                let n: usize = parts.next().unwrap().parse().unwrap();
                let values: Vec<i32> = parts.take(n).map(|t| t.parse().unwrap()).collect();
                println!("{}", last_index_of(&values, target));
            }
            "clean" => {
                let n: usize = parts.next().unwrap().parse().unwrap();
                let mut values: Vec<i32> = parts.take(n).map(|t| t.parse().unwrap()).collect();
                remove_negatives(&mut values);
                if values.is_empty() {
                    println!("empty");
                } else {
                    let texts: Vec<String> = values.iter().map(|v| v.to_string()).collect();
                    println!("{}", texts.join(" "));
                }
            }
            "words" => {
                let n: usize = parts.next().unwrap().parse().unwrap();
                let words: Vec<String> = parts.take(n).map(|t| t.to_string()).collect();
                let counts = count_words(&words);
                let mut names: Vec<&String> = counts.keys().collect();
                names.sort();
                for name in names {
                    println!("{}={}", name, counts[name]);
                }
            }
            "mid" => {
                let arg = parts.next().unwrap_or("-");
                let word = if arg == "-" { "" } else { arg };
                println!("[{}]", middle(word));
            }
            _ => {}
        }
    }
}
