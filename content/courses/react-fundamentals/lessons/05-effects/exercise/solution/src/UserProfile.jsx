import { useEffect, useState } from "react";

export function UserProfile({ userId, fetchUser }) {
  const [state, setState] = useState({ status: "loading" });

  useEffect(() => {
    const controller = new AbortController();
    let ignore = false; // защита от устаревшего ответа: fetchUser мог не поддержать отмену
    setState({ status: "loading" });
    fetchUser(userId, controller.signal).then(
      (user) => {
        if (!ignore) setState({ status: "done", user });
      },
      (error) => {
        if (!ignore) setState({ status: "error", message: error.message });
      },
    );
    return () => {
      ignore = true;
      controller.abort();
    };
  }, [userId, fetchUser]);

  if (state.status === "loading") return <p>Загрузка...</p>;
  if (state.status === "error") return <p role="alert">Ошибка: {state.message}</p>;
  return <h2>{state.user.name}</h2>;
}
