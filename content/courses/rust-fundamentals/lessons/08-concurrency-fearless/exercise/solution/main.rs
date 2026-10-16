use std::io::{self, Read};
use std::sync::mpsc;
use std::sync::{Arc, Mutex};
use std::thread;

/// Делит данные на не более чем `workers` подряд идущих кусков (размер куска - потолок деления).
fn split(data: &[u64], workers: usize) -> Vec<&[u64]> {
    if data.is_empty() || workers == 0 {
        return Vec::new();
    }
    let size = data.len().div_ceil(workers);
    data.chunks(size).collect()
}

/// Каждый кусок обрабатывается в своём потоке (scope); результат - (индекс, сумма квадратов).
/// Счётчик `processed` общий: Arc<Mutex<usize>>.
fn run(chunks: &[&[u64]], processed: &Arc<Mutex<usize>>) -> Vec<(usize, u64)> {
    let (tx, rx) = mpsc::channel();
    thread::scope(|s| {
        for (i, chunk) in chunks.iter().enumerate() {
            let tx = tx.clone();
            let processed = Arc::clone(processed);
            s.spawn(move || {
                let sum: u64 = chunk.iter().map(|x| x * x).sum();
                *processed.lock().unwrap() += chunk.len();
                tx.send((i, sum)).unwrap();
            });
        }
    });
    drop(tx);
    let mut results: Vec<(usize, u64)> = rx.iter().collect();
    results.sort();
    results
}

fn main() {
    let mut input = String::new();
    io::stdin().read_to_string(&mut input).unwrap();
    let mut tokens = input.split_whitespace();
    let workers: usize = tokens.next().and_then(|t| t.parse().ok()).unwrap_or(1);
    let data: Vec<u64> = tokens.filter_map(|t| t.parse().ok()).collect();

    let chunks = split(&data, workers);
    let processed = Arc::new(Mutex::new(0usize));
    let results = run(&chunks, &processed);

    for (i, sum) in &results {
        println!("chunk {i}: {sum}");
    }
    let total: u64 = results.iter().map(|(_, s)| s).sum();
    println!("total={total} processed={}", processed.lock().unwrap());
}
