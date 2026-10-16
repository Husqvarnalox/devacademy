export interface Point2D {
  x: number;
  y: number;
}

export function distance(a: Point2D, b: Point2D): number {
  return Math.hypot(a.x - b.x, a.y - b.y);
}

export function centroid(points: readonly Point2D[]): Point2D | undefined {
  if (points.length === 0) return undefined;
  let sx = 0;
  let sy = 0;
  for (const p of points) {
    sx += p.x;
    sy += p.y;
  }
  return { x: sx / points.length, y: sy / points.length };
}

export function translate(p: Point2D, dx: number, dy: number): Point2D {
  return { x: p.x + dx, y: p.y + dy };
}

export function nearest(points: readonly Point2D[], target: Point2D): Point2D | undefined {
  let best: Point2D | undefined;
  let bestDistance = Infinity;
  for (const p of points) {
    const d = distance(p, target);
    if (d < bestDistance) {
      best = p;
      bestDistance = d;
    }
  }
  return best;
}
