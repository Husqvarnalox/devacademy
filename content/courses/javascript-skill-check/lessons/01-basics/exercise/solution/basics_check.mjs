export function fizzBuzz(n) {
  if (n % 15 === 0) return "FizzBuzz";
  if (n % 3 === 0) return "Fizz";
  if (n % 5 === 0) return "Buzz";
  return String(n);
}

export function sumOfMultiples(limit) {
  let total = 0;
  for (let i = 1; i < limit; i++) {
    if (i % 3 === 0 || i % 5 === 0) total += i;
  }
  return total;
}

export function isLeapYear(year) {
  return (year % 4 === 0 && year % 100 !== 0) || year % 400 === 0;
}

function isPrime(k) {
  if (k < 2) return false;
  for (let d = 2; d * d <= k; d++) {
    if (k % d === 0) return false;
  }
  return true;
}

export function firstPrimeAbove(n) {
  let candidate = n + 1;
  while (!isPrime(candidate)) candidate++;
  return candidate;
}
