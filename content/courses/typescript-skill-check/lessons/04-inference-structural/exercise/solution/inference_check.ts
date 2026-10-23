export interface Person {
  name: string;
  age: number;
  email?: string;
}

export function greet(who: { name: string }): string {
  return "Здравствуйте, " + who.name + "!";
}

export function describe(entity: { name: string; age?: number }): string {
  if (entity.age === undefined) {
    return entity.name;
  }
  return entity.name + " (" + entity.age + ")";
}

export function adultNames(people: readonly Person[]): string[] {
  return people.filter((person) => person.age >= 18).map((person) => person.name);
}

export function oldest(people: readonly Person[]): Person | undefined {
  let best: Person | undefined = undefined;
  for (const person of people) {
    if (best === undefined || person.age > best.age) {
      best = person;
    }
  }
  return best;
}
