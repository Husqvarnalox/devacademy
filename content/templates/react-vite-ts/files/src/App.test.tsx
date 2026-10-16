import { render, screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { App } from "./App";

describe("App", () => {
  it("увеличивает счётчик по клику", async () => {
    render(<App />);
    expect(screen.getByText("Нажатий: 0")).toBeInTheDocument();
    await userEvent.click(screen.getByRole("button", { name: "Увеличить" }));
    expect(screen.getByText("Нажатий: 1")).toBeInTheDocument();
  });
});
