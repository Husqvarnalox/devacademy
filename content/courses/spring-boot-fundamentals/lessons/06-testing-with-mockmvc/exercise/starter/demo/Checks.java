package demo;

import org.springframework.test.web.servlet.MockMvc;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

class Checks {
    // Called once per variant of the application. Throw an AssertionError (any failed andExpect does it) when the
    // behaviour differs from the specification in the task. Nothing to throw for the correct variant.
    static void run(MockMvc mvc) throws Exception {
        // TODO: проверьте поведение из условия: GET /products, GET /products/1, GET /products/99, POST /products
        // (корректный и некорректный запрос). Пример:
        // mvc.perform(get("/products/99")).andExpect(status().isNotFound());
        // Тело JSON: .andExpect(jsonPath("$.name").value("Tea"))
    }
}
