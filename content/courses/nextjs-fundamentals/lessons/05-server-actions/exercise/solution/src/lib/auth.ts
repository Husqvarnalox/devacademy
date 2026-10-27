// Учебная замена настоящей сессии. В приложении пользователя определяют по cookie или токену,
// здесь «текущий пользователь» хранится в переменной, чтобы тесты могли его подменять.
export type User = { id: string; name: string };

let current: User | null = null;

export async function getCurrentUser(): Promise<User | null> {
  return current;
}

export function setCurrentUser(user: User | null): void {
  current = user;
}
