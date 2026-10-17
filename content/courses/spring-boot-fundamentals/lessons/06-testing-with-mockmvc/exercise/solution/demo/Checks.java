package demo;

import org.springframework.http.MediaType;
import org.springframework.test.web.servlet.MockMvc;

import static org.springframework.test.web.servlet.request.MockMvcRequestBuilders.*;
import static org.springframework.test.web.servlet.result.MockMvcResultMatchers.*;

class Checks {
    static void run(MockMvc mvc) throws Exception {
        mvc.perform(get("/products"))
                .andExpect(status().isOk())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON))
                .andExpect(jsonPath("$.length()").value(2))
                .andExpect(jsonPath("$[0].name").value("Tea"))
                .andExpect(jsonPath("$[1].price").value(400));

        mvc.perform(get("/products/1"))
                .andExpect(status().isOk())
                .andExpect(content().contentTypeCompatibleWith(MediaType.APPLICATION_JSON))
                .andExpect(jsonPath("$.id").value(1))
                .andExpect(jsonPath("$.name").value("Tea"))
                .andExpect(jsonPath("$.price").value(250));

        mvc.perform(get("/products/99")).andExpect(status().isNotFound());

        mvc.perform(post("/products").contentType(MediaType.APPLICATION_JSON).content("{\"name\":\"Cocoa\",\"price\":300}"))
                .andExpect(status().isCreated())
                .andExpect(header().string("Location", "/products/3"))
                .andExpect(jsonPath("$.id").value(3))
                .andExpect(jsonPath("$.name").value("Cocoa"))
                .andExpect(jsonPath("$.price").value(300));

        mvc.perform(post("/products").contentType(MediaType.APPLICATION_JSON).content("{\"name\":\"\",\"price\":300}"))
                .andExpect(status().isBadRequest());
        mvc.perform(post("/products").contentType(MediaType.APPLICATION_JSON).content("{\"name\":\"Free\",\"price\":0}"))
                .andExpect(status().isBadRequest());
    }
}
