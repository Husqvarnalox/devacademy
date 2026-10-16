import { useState } from "react";

export function SignupForm({ onSubmit }) {
  const [name, setName] = useState("");
  const [email, setEmail] = useState("");
  const [error, setError] = useState("");

  function handleSubmit(event) {
    event.preventDefault(); // иначе браузер перезагрузит страницу
    const cleanName = name.trim();
    const cleanEmail = email.trim();
    if (cleanName === "") {
      setError("Введите имя");
      return;
    }
    if (!cleanEmail.includes("@")) {
      setError("Некорректный email");
      return;
    }
    setError("");
    onSubmit({ name: cleanName, email: cleanEmail });
    setName("");
    setEmail("");
  }

  return (
    <form onSubmit={handleSubmit}>
      <label>
        Имя
        <input value={name} onChange={(e) => setName(e.target.value)} />
      </label>
      <label>
        Email
        <input value={email} onChange={(e) => setEmail(e.target.value)} />
      </label>
      {error && <p role="alert">{error}</p>}
      <button type="submit">Отправить</button>
    </form>
  );
}
