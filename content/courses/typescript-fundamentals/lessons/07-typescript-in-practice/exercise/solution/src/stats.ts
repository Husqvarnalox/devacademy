import { clamp, mean, VERSION } from "../vendor/legacy-math.js";

export function clampAll(xs: readonly number[], lo: number, hi: number): number[] {
  return xs.map((x) => clamp(x, lo, hi));
}

export function average(xs: readonly number[]): number {
  return mean(xs);
}

export function libVersion(): string {
  return VERSION;
}
