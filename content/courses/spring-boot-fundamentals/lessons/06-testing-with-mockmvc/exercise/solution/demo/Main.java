package demo;

import java.nio.charset.StandardCharsets;
import java.util.ArrayList;
import java.util.List;
import org.springframework.boot.ApplicationContextFactory;
import org.springframework.boot.Banner;
import org.springframework.boot.WebApplicationType;
import org.springframework.boot.builder.SpringApplicationBuilder;
import org.springframework.context.ConfigurableApplicationContext;
import org.springframework.mock.web.MockServletContext;
import org.springframework.test.web.servlet.MockMvc;
import org.springframework.test.web.servlet.setup.MockMvcBuilders;
import org.springframework.web.context.WebApplicationContext;
import org.springframework.web.context.support.GenericWebApplicationContext;

// Check driver: do not edit. Every input line names a variant of the application (good, bug-status, ...).
// For each variant it starts the Spring Boot application WITHOUT a web server (no port is opened) with
// --app.variant=<name>, calls Checks.run(mvc) and prints "<variant>: PASS" if nothing was thrown or
// "<variant>: FAIL" if an AssertionError was thrown (any other exception prints "<variant>: ERROR <class>").
public class Main {
    public static void main(String[] args) throws Exception {
        try {
            serve(args);
        } catch (Exception e) {
            startupFailed(e);
        }
    }

    private static void serve(String[] args) throws Exception {
        List<String> variants = new ArrayList<>();
        for (String line : new String(System.in.readAllBytes(), StandardCharsets.UTF_8).split("\n")) {
            if (!line.isBlank()) variants.add(line.strip());
        }
        for (String variant : variants) {
            System.out.println(variant + ": " + check(variant));
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

    private static String check(String variant) throws Exception {
        try (ConfigurableApplicationContext context = new SpringApplicationBuilder(App.class)
                .web(WebApplicationType.SERVLET)
                .bannerMode(Banner.Mode.OFF)
                .logStartupInfo(false)
                .contextFactory(ApplicationContextFactory.of(() -> new GenericWebApplicationContext(new MockServletContext())))
                .run("--logging.level.root=OFF", "--spring.main.banner-mode=off", "--app.variant=" + variant)) {
            MockMvc mvc = MockMvcBuilders.webAppContextSetup((WebApplicationContext) context).build();
            try {
                Checks.run(mvc);
                return "PASS";
            } catch (AssertionError e) {
                return "FAIL";
            } catch (Exception e) {
                return "ERROR " + e.getClass().getSimpleName();
            }
        }
    }
}
