use std::io::{self, Read};
use std::sync::mpsc;
use std::sync::{Arc, Mutex};
use std::thread;

/// Число шагов гипотезы Коллатца до единицы.
fn collatz(mut n: u64) -> u32 {
    let mut steps = 0;
    while n != 1 {
        n = if n % 2 == 0 { n / 2 } else { 3 * n + 1 };
        steps += 1;
    }
    steps
}

/// Пул из `workers` потоков берёт задачи из общей очереди.
/// Возвращает (индекс, n, шаги) по порядку индексов.
fn pool(jobs: Vec<u64>, workers: usize) -> Vec<(usize, u64, u32)> {
    let (job_tx, job_rx) = mpsc::channel::<(usize, u64)>();
    let (res_tx, res_rx) = mpsc::channel();
    let job_rx = Arc::new(Mutex::new(job_rx));

    let handles: Vec<_> = (0..workers)
        .map(|_| {
            let job_rx = Arc::clone(&job_rx);
            let res_tx = res_tx.clone();
            thread::spawn(move || loop {
                let job = job_rx.lock().unwrap().recv();
                match job {
                    Ok((i, n)) => res_tx.send((i, n, collatz(n))).unwrap(),
                    Err(_) => break,
                }
            })
        })
        .collect();
    drop(res_tx);

    for (i, n) in jobs.into_iter().enumerate() {
        job_tx.send((i, n)).unwrap();
    }
    drop(job_tx);

    let mut results: Vec<(usize, u64, u32)> = res_rx.iter().collect();
    for h in handles {
        h.join().unwrap();
    }
    results.sort();
    results
}

fn main() {
    let mut input = String::new();
    io::stdin().read_to_string(&mut input).unwrap();
    let mut tokens = input.split_whitespace();
    let workers: usize = tokens.next().and_then(|t| t.parse().ok()).unwrap_or(1).max(1);
    let jobs: Vec<u64> = tokens.filter_map(|t| t.parse().ok()).filter(|&n| n >= 1).collect();

    let results = pool(jobs, workers);
    for (i, n, steps) in &results {
        println!("job {i}: {n} -> {steps}");
    }
    let best = results.iter().max_by_key(|(i, _, s)| (*s, std::cmp::Reverse(*i)));
    match best {
        Some((i, n, s)) => println!("longest: job {i} ({n}) with {s} steps"),
        None => println!("no jobs"),
    }
}
