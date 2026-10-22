use std::collections::HashMap;

struct Record {
    name: String,
    score: i32,
}

struct Stats {
    sum: i32,
    count: i32,
}

impl Stats {
    fn average(&self) -> f64 {
        self.sum as f64 / self.count as f64
    }
}

fn parse_record(line: &str) -> Result<Record, String> {
    match line.split_once(';') {
        Some((name_text, score_text)) => {
            let name = name_text.trim();
            if name == "" {
                return Err(String::from("пустое имя"));
            }
            match score_text.trim().parse::<i32>() {
                Ok(score) => {
                    if score >= 0 && score <= 100 {
                        Ok(Record { name: String::from(name), score })
                    } else {
                        Err(format!("балл вне диапазона: {score}"))
                    }
                }
                Err(_) => Err(format!("не число: {}", score_text.trim())),
            }
        }
        None => Err(String::from("нет разделителя")),
    }
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut table: HashMap<String, Stats> = HashMap::new();
    let mut valid = 0;
    let mut bad = 0;

    for raw in input.lines() {
        let line = raw.trim();
        if line == "" {
            continue;
        }
        match parse_record(line) {
            Ok(rec) => {
                let stats = table.entry(rec.name).or_insert(Stats { sum: 0, count: 0 });
                stats.sum += rec.score;
                stats.count += 1;
                valid += 1;
            }
            Err(_) => bad += 1,
        }
    }

    let mut names: Vec<&String> = Vec::new();
    for name in table.keys() {
        names.push(name);
    }
    names.sort();

    let mut best_name = String::new();
    let mut best_avg = -1.0;
    for name in names {
        let avg = table[name].average();
        println!("{name}: {:.2}", avg);
        if avg > best_avg {
            best_avg = avg;
            best_name = name.clone();
        }
    }

    println!("Записей: {valid}");
    if valid == 0 {
        println!("Лучший: нет");
    } else {
        println!("Лучший: {best_name}");
    }
    println!("Неверных строк: {bad}");
}
