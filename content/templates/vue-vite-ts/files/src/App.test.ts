import { mount } from "@vue/test-utils";
import { describe, expect, it } from "vitest";
import App from "./App.vue";

describe("App", () => {
  it("увеличивает счётчик по клику", async () => {
    const wrapper = mount(App);
    expect(wrapper.text()).toContain("Нажатий: 0");
    await wrapper.get("button").trigger("click");
    expect(wrapper.text()).toContain("Нажатий: 1");
  });
});
