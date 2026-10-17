package demo;

import jakarta.servlet.ServletException;
import java.io.IOException;
import java.net.URI;
import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.http.HttpMethod;
import org.springframework.mock.web.MockHttpServletResponse;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.request.MockHttpServletRequestBuilder;
import org.springframework.test.web.servlet.request.MockMvcRequestBuilders;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

// Check driver: do not edit. It starts the Spring Boot application WITHOUT a web server (no port is opened),
// feeds the requests from stdin straight into the DispatcherServlet through MockMvc and prints the responses.
// One input line is one request:   METHOD /path [| h:Name=value] [| b:body]
// Lines that start with "@" are settings applied before the application starts:   @prop key=value   @profile name
public class Main {
    public static void main(String[] args) throws Exception {
        try {
            serve();
        } catch (Exception e) {
            startupFailed(e);
        }
    }

    private static void serve() throws Exception {
        List<String> settings = new ArrayList<>();
        List<String> requests = new ArrayList<>();
        for (String line : new String(System.in.readAllBytes(), StandardCharsets.UTF_8).split("\n")) {
            String text = line.strip();
            if (text.isEmpty()) continue;
            if (text.startsWith("@prop ")) settings.add("--" + text.substring(6).strip());
            else if (text.startsWith("@profile ")) settings.add("--spring.profiles.active=" + text.substring(9).strip());
            else requests.add(text);
        }
        settings.add("--logging.level.root=OFF");
        settings.add("--spring.main.banner-mode=off");

        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(App.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run(settings.toArray(String[]::new))) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            for (String request : requests) send(mvc, request);
        }
    }

    // Prints the reason a context did not start (to stderr, which the checker shows) and fails the run:
    //   startup failed: <ExceptionClass>: <first line of the root cause message>
    private static void startupFailed(Throwable e) {
        Throwable root = e;
        while (root.getCause() != null && root.getCause() != root) root = root.getCause();
        String message = root.getMessage() == null ? "" : root.getMessage().strip().split("\\R", 2)[0];
        System.err.println("startup failed: " + e.getClass().getSimpleName() + ": " + message);
        System.exit(1);
    }

    private static void send(MockMvc mvc, String line) throws IOException {
        String[] parts = line.split(" \\| ");
        String[] head = parts[0].split(" ", 2);
        MockHttpServletRequestBuilder builder = MockMvcRequestBuilders.request(HttpMethod.valueOf(head[0]), URI.create(head[1].replace(" ", "%20")));
        for (int i = 1; i < parts.length; i++) {
            if (parts[i].startsWith("h:")) {
                String[] header = parts[i].substring(2).split("=", 2);
                builder.header(header[0], header[1]);
            } else if (parts[i].startsWith("b:")) {
                builder.contentType("application/json").content(parts[i].substring(2).getBytes(StandardCharsets.UTF_8));
            }
        }
        try {
            MockHttpServletResponse response = mvc.perform(builder).andReturn().getResponse();
            String type = response.getContentType() == null ? "-" : response.getContentType().split(";")[0];
            System.out.println((response.getStatus() + " " + type + " " + response.getContentAsString(StandardCharsets.UTF_8)).stripTrailing());
            if (response.getHeader("Location") != null) System.out.println("  Location: " + response.getHeader("Location"));
            if (response.getHeader("ETag") != null) System.out.println("  ETag: " + response.getHeader("ETag"));
        } catch (Exception e) {
            // A real server would answer 500 for an exception nobody handled; the class tells you what escaped.
            Throwable cause = e;
            while (cause instanceof ServletException && cause.getCause() != null) cause = cause.getCause();
            System.out.println("500 - " + cause.getClass().getSimpleName());
        }
    }
}
