use std::collections::HashMap;

fn last_index_of(values: &Vec<i32>, target: i32) -> i32 {
    // TODO: верните индекс последнего вхождения target или -1
    -2
}

fn remove_negatives(values: &mut Vec<i32>) {
    // TODO: оставьте в векторе только неотрицательные числа в прежнем порядке
}

fn count_words(words: &Vec<String>) -> HashMap<String, i32> {
    // TODO: посчитайте, сколько раз встречается каждое слово
    HashMap::new()
}

fn middle(text: &str) -> String {
    // TODO: верните среднюю букву (нечётная длина), две средние (чётная)
    // или пустую строку; считайте буквы, а не байты
    String::from("?")
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
