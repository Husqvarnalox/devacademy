fn count_above(values: &Vec<i32>, limit: i32) -> i32 {
    let mut count = 0;
    for v in values {
        if *v > limit {
            count += 1;
        }
    }
    count
}

fn largest(values: &Vec<i32>) -> Option<i32> {
    if values.len() == 0 {
        return None;
    }
    let mut best = values[0];
    for v in values {
        if *v > best {
            best = *v;
        }
    }
    Some(best)
}

fn main() {
    let input = std::io::read_to_string(std::io::stdin()).unwrap();
    let mut lines = input.lines();
    let limit = lines.next().unwrap_or("0").trim().parse::<i32>().unwrap_or(0);
    let mut temps: Vec<i32> = Vec::new();
    for line in lines {
        if let Ok(t) = line.trim().parse::<i32>() {
            temps.push(t);
        }
    }
    println!("Измерений: {}", temps.len());
    println!("Выше {limit}: {}", count_above(&temps, limit));
    match largest(&temps) {
        Some(m) => println!("Максимум: {m}"),
        None => println!("Максимум: нет данных"),
    }
}
