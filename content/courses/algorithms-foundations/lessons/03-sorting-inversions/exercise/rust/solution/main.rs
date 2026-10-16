use std::io::{self, Read};

// Сортирует a на месте (tmp - буфер той же длины) и возвращает число пар i < j с a[i] > a[j].
fn sort_count(a: &mut [i64], tmp: &mut [i64]) -> i64 {
    let n = a.len();
    if n <= 1 {
        return 0;
    }
    let mid = n / 2;
    let mut inv = {
        let (al, ar) = a.split_at_mut(mid);
        let (tl, tr) = tmp.split_at_mut(mid);
        sort_count(al, tl) + sort_count(ar, tr)
    };
    let (mut i, mut j, mut k) = (0, mid, 0);
    while i < mid && j < n {
        if a[i] <= a[j] {
            tmp[k] = a[i];
            i += 1;
        } else {
            tmp[k] = a[j];
            j += 1;
            inv += (mid - i) as i64; // a[j] меньше всех оставшихся слева
        }
        k += 1;
    }
    while i < mid {
        tmp[k] = a[i];
        i += 1;
        k += 1;
    }
    while j < n {
        tmp[k] = a[j];
        j += 1;
        k += 1;
    }
    a.copy_from_slice(&tmp[..n]);
    inv
}

fn main() {
    let mut s = String::new();
    io::stdin().read_to_string(&mut s).unwrap();
    let mut it = s.split_ascii_whitespace();
    let n: usize = it.next().unwrap().parse().unwrap();
    let mut a: Vec<i64> = it.take(n).map(|x| x.parse().unwrap()).collect();
    let mut tmp = vec![0i64; n];
    println!("{}", sort_count(&mut a, &mut tmp));
}
