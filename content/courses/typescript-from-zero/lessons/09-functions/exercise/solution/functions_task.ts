export function farewell(name: string): string {
  return "До свидания, " + name + "!";
}

export function lineTotal(price: number, quantity: number): number {
  return price * quantity;
}

export function discounted(price: number, percent: number): number {
  return (price * (100 - percent)) / 100;
}
