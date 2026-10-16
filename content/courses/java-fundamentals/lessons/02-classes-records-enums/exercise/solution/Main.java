import java.io.BufferedReader;
import java.io.IOException;
import java.io.InputStreamReader;
import java.util.ArrayList;
import java.util.HashSet;
import java.util.List;
import java.util.Locale;
import java.util.Set;

public class Main {
    enum Stage {
        ALPHA, BETA, RELEASE;

        static Stage parse(String s) {
            return switch (s) {
                case "alpha" -> ALPHA;
                case "beta" -> BETA;
                default -> throw new IllegalArgumentException("stage: " + s);
            };
        }

        String suffix() {
            return this == RELEASE ? "" : "-" + name().toLowerCase(Locale.ROOT);
        }
    }

    record Version(int major, int minor, int patch, Stage stage) implements Comparable<Version> {
        static final int LIMIT = 1_000_000;

        Version {
            if (major > LIMIT || minor > LIMIT || patch > LIMIT) {
                throw new IllegalArgumentException("too large");
            }
            if (major < 0 || minor < 0 || patch < 0) {
                throw new IllegalArgumentException("negative");
            }
        }

        static Version parse(String line) {
            String text = line.trim();
            int dash = text.indexOf('-');
            Stage stage = dash < 0 ? Stage.RELEASE : Stage.parse(text.substring(dash + 1));
            String core = dash < 0 ? text : text.substring(0, dash);
            String[] parts = core.split("\\.", -1);
            if (parts.length != 3) {
                throw new IllegalArgumentException("parts");
            }
            int[] n = new int[3];
            for (int i = 0; i < 3; i++) {
                if (!parts[i].matches("[0-9]{1,7}")) {
                    throw new IllegalArgumentException("number");
                }
                n[i] = Integer.parseInt(parts[i]);
            }
            return new Version(n[0], n[1], n[2], stage);
        }

        @Override
        public int compareTo(Version o) {
            int c = Integer.compare(major, o.major);
            if (c == 0) c = Integer.compare(minor, o.minor);
            if (c == 0) c = Integer.compare(patch, o.patch);
            if (c == 0) c = stage.compareTo(o.stage);
            return c;
        }

        @Override
        public String toString() {
            return major + "." + minor + "." + patch + stage.suffix();
        }
    }

    public static void main(String[] args) throws IOException {
        BufferedReader in = new BufferedReader(new InputStreamReader(System.in));
        int n = Integer.parseInt(in.readLine().trim());
        Set<Version> seen = new HashSet<>();
        for (int i = 0; i < n; i++) {
            String line = in.readLine();
            try {
                seen.add(Version.parse(line));
            } catch (IllegalArgumentException e) {
                System.out.println("invalid: " + line);
            }
        }
        List<Version> sorted = new ArrayList<>(seen);
        sorted.sort(null);
        for (Version v : sorted) {
            System.out.println(v);
        }
        System.out.println("distinct=" + sorted.size());
        if (!sorted.isEmpty()) {
            System.out.println("latest=" + sorted.get(sorted.size() - 1));
        }
    }
}
