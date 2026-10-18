import com.sun.jdi.AbsentInformationException;
import com.sun.jdi.ArrayReference;
import com.sun.jdi.Bootstrap;
import com.sun.jdi.ClassNotPreparedException;
import com.sun.jdi.Field;
import com.sun.jdi.IncompatibleThreadStateException;
import com.sun.jdi.LocalVariable;
import com.sun.jdi.Location;
import com.sun.jdi.Method;
import com.sun.jdi.ObjectReference;
import com.sun.jdi.PrimitiveValue;
import com.sun.jdi.ReferenceType;
import com.sun.jdi.StackFrame;
import com.sun.jdi.StringReference;
import com.sun.jdi.ThreadReference;
import com.sun.jdi.Value;
import com.sun.jdi.VMDisconnectedException;
import com.sun.jdi.VirtualMachine;
import com.sun.jdi.connect.AttachingConnector;
import com.sun.jdi.connect.Connector;
import com.sun.jdi.event.BreakpointEvent;
import com.sun.jdi.event.ClassPrepareEvent;
import com.sun.jdi.event.Event;
import com.sun.jdi.event.EventSet;
import com.sun.jdi.event.ExceptionEvent;
import com.sun.jdi.event.StepEvent;
import com.sun.jdi.event.VMDeathEvent;
import com.sun.jdi.event.VMDisconnectEvent;
import com.sun.jdi.event.VMStartEvent;
import com.sun.jdi.request.BreakpointRequest;
import com.sun.jdi.request.ClassPrepareRequest;
import com.sun.jdi.request.EventRequest;
import com.sun.jdi.request.EventRequestManager;
import com.sun.jdi.request.StepRequest;
import java.io.BufferedReader;
import java.io.ByteArrayOutputStream;
import java.io.IOException;
import java.io.InputStream;
import java.io.InputStreamReader;
import java.io.OutputStream;
import java.nio.charset.StandardCharsets;
import java.nio.file.Files;
import java.nio.file.Path;
import java.util.ArrayList;
import java.util.HashMap;
import java.util.LinkedHashMap;
import java.util.List;
import java.util.Map;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.TimeUnit;
import java.util.regex.Matcher;
import java.util.regex.Pattern;

/**
 * DevAcademy's Java debug adapter: a small Debug Adapter Protocol server over stdio on top of JDI (jdk.jdi, part of every JDK).
 * It starts the program in a child JVM with a JDWP agent that listens on 127.0.0.1 only, attaches to it and translates.
 * Built once with the local javac; nothing is downloaded. Single file by design: it is compiled with {@code javac File.java}.
 */
public final class DevAcademyJdiAdapter {
    public static void main(String[] args) throws Exception {
        OutputStream dapOut = System.out;
        System.setOut(System.err);   // nothing but DAP messages may reach the protocol stream
        new DevAcademyJdiAdapter(System.in, dapOut).run();
        System.exit(0);
    }

    // ------------------------------------------------------------------ JSON (just enough for DAP)

    static final class Json {
        private final String s;
        private int i;

        private Json(String s) { this.s = s; }

        static Object parse(String text) {
            Json p = new Json(text);
            Object v = p.value();
            return v;
        }

        private void ws() { while (i < s.length() && Character.isWhitespace(s.charAt(i))) i++; }

        private Object value() {
            ws();
            char c = s.charAt(i);
            switch (c) {
                case '{': {
                    i++;
                    Map<String, Object> m = new LinkedHashMap<>();
                    ws();
                    if (s.charAt(i) == '}') { i++; return m; }
                    while (true) {
                        ws();
                        String k = string();
                        ws();
                        i++;   // ':'
                        m.put(k, value());
                        ws();
                        if (s.charAt(i++) == '}') return m;
                    }
                }
                case '[': {
                    i++;
                    List<Object> l = new ArrayList<>();
                    ws();
                    if (s.charAt(i) == ']') { i++; return l; }
                    while (true) {
                        l.add(value());
                        ws();
                        if (s.charAt(i++) == ']') return l;
                    }
                }
                case '"': return string();
                case 't': i += 4; return Boolean.TRUE;
                case 'f': i += 5; return Boolean.FALSE;
                case 'n': i += 4; return null;
                default: {
                    int st = i;
                    while (i < s.length() && "+-0123456789.eE".indexOf(s.charAt(i)) >= 0) i++;
                    String n = s.substring(st, i);
                    return n.contains(".") || n.contains("e") || n.contains("E") ? (Object) Double.valueOf(n) : (Object) Long.valueOf(n);
                }
            }
        }

        private String string() {
            StringBuilder b = new StringBuilder();
            i++;   // opening quote
            while (true) {
                char c = s.charAt(i++);
                if (c == '"') return b.toString();
                if (c != '\\') { b.append(c); continue; }
                char e = s.charAt(i++);
                switch (e) {
                    case 'n': b.append('\n'); break;
                    case 't': b.append('\t'); break;
                    case 'r': b.append('\r'); break;
                    case 'b': b.append('\b'); break;
                    case 'f': b.append('\f'); break;
                    case 'u': b.append((char) Integer.parseInt(s.substring(i, i + 4), 16)); i += 4; break;
                    default: b.append(e);
                }
            }
        }

        static String write(Object o) {
            StringBuilder b = new StringBuilder();
            write(b, o);
            return b.toString();
        }

        @SuppressWarnings("unchecked")
        private static void write(StringBuilder b, Object o) {
            if (o == null) b.append("null");
            else if (o instanceof String str) quote(b, str);
            else if (o instanceof Map<?, ?> m) {
                b.append('{');
                boolean first = true;
                for (Map.Entry<String, Object> e : ((Map<String, Object>) m).entrySet()) {
                    if (!first) b.append(',');
                    first = false;
                    quote(b, e.getKey());
                    b.append(':');
                    write(b, e.getValue());
                }
                b.append('}');
            } else if (o instanceof List<?> l) {
                b.append('[');
                for (int k = 0; k < l.size(); k++) {
                    if (k > 0) b.append(',');
                    write(b, l.get(k));
                }
                b.append(']');
            } else b.append(o);   // numbers and booleans
        }

        private static void quote(StringBuilder b, String str) {
            b.append('"');
            for (int k = 0; k < str.length(); k++) {
                char c = str.charAt(k);
                switch (c) {
                    case '"': b.append("\\\""); break;
                    case '\\': b.append("\\\\"); break;
                    case '\n': b.append("\\n"); break;
                    case '\r': b.append("\\r"); break;
                    case '\t': b.append("\\t"); break;
                    default:
                        if (c < 0x20) b.append(String.format("\\u%04x", (int) c)); else b.append(c);
                }
            }
            b.append('"');
        }
    }

    static Map<String, Object> obj(Object... kv) {
        Map<String, Object> m = new LinkedHashMap<>();
        for (int k = 0; k < kv.length; k += 2) m.put((String) kv[k], kv[k + 1]);
        return m;
    }

    @SuppressWarnings("unchecked")
    static Map<String, Object> map(Object o) { return o instanceof Map ? (Map<String, Object>) o : new LinkedHashMap<>(); }

    @SuppressWarnings("unchecked")
    static List<Object> list(Object o) { return o instanceof List ? (List<Object>) o : new ArrayList<>(); }

    static String str(Map<String, Object> m, String k) { return m.get(k) instanceof String v ? v : null; }

    static int num(Map<String, Object> m, String k, int def) { return m.get(k) instanceof Number v ? v.intValue() : def; }

    // ------------------------------------------------------------------ state

    /** A requested breakpoint and the JDI requests that realise it. */
    private static final class Bp {
        final int id;
        final String path;
        final int line;
        int resolvedLine;
        boolean verified;
        final List<BreakpointRequest> requests = new ArrayList<>();

        Bp(int id, String path, int line) { this.id = id; this.path = path; this.line = line; resolvedLine = line; }
    }

    /** What a variablesReference stands for. */
    private record Locals(StackFrame frame) { }
    private record Fields(ObjectReference object) { }
    private record Elements(ArrayReference array) { }

    private static final Pattern LISTENING = Pattern.compile("address:\\s*(?:\\S*:)?(\\d+)");
    private static final String[] EXCLUDED = { "java.*", "javax.*", "jdk.*", "sun.*", "com.sun.*" };

    private final InputStream in;
    private final OutputStream out;
    private int seq;
    private VirtualMachine vm;
    private Process process;
    private final List<Thread> pumps = new ArrayList<>();
    private final Map<String, List<Bp>> breakpoints = new HashMap<>();
    private int nextBpId = 1;
    private final List<String> sourceRoots = new ArrayList<>();
    private final Map<Integer, Object> handles = new HashMap<>();   // valid while stopped; cleared on every resume
    private final Map<Integer, StackFrame> frames = new HashMap<>();
    private int nextHandle = 1;
    private volatile boolean ending;
    private boolean terminatedSent;
    private boolean launched;
    // The JVM is launched with suspend=y and JDI delivers a VMStartEvent set that suspends everything. Resuming that set (the
    // default for an event set nobody handles) would start the program as soon as the event loop runs, before the client has sent
    // its breakpoints: a fast JVM then runs past them. The set is held until configurationDone.
    private final Object startGate = new Object();
    private EventSet heldStart;
    private boolean configured;

    private DevAcademyJdiAdapter(InputStream in, OutputStream out) {
        this.in = in;
        this.out = out;
    }

    // ------------------------------------------------------------------ transport

    private String readMessage() throws IOException {
        int length = -1;
        while (true) {
            ByteArrayOutputStream line = new ByteArrayOutputStream();
            int b;
            while ((b = in.read()) != -1 && b != '\n') if (b != '\r') line.write(b);
            if (b == -1 && line.size() == 0) return null;
            String h = line.toString(StandardCharsets.US_ASCII);
            if (h.isEmpty()) break;
            if (h.regionMatches(true, 0, "Content-Length:", 0, 15)) length = Integer.parseInt(h.substring(15).trim());
        }
        if (length < 0) return null;
        byte[] body = in.readNBytes(length);
        return body.length < length ? null : new String(body, StandardCharsets.UTF_8);
    }

    private synchronized void send(Map<String, Object> message) {
        message.put("seq", ++seq);
        byte[] body = Json.write(message).getBytes(StandardCharsets.UTF_8);
        try {
            out.write(("Content-Length: " + body.length + "\r\n\r\n").getBytes(StandardCharsets.US_ASCII));
            out.write(body);
            out.flush();
        } catch (IOException e) {
            System.exit(1);   // the client is gone
        }
    }

    private void event(String name, Map<String, Object> body) { send(obj("type", "event", "event", name, "body", body)); }

    private void respond(Map<String, Object> request, Object body) {
        send(obj("type", "response", "request_seq", request.get("seq"), "success", true, "command", request.get("command"), "body", body));
    }

    private void fail(Map<String, Object> request, String message) {
        send(obj("type", "response", "request_seq", request.get("seq"), "success", false, "command", request.get("command"),
                "message", message, "body", obj("error", obj("id", 1, "format", message))));
    }

    private void output(String category, String text) { event("output", obj("category", category, "output", text)); }

    // ------------------------------------------------------------------ request loop

    private void run() throws Exception {
        String text;
        while ((text = readMessage()) != null) {
            Map<String, Object> request = map(Json.parse(text));
            if (!"request".equals(request.get("type"))) continue;
            try {
                if (handle(str(request, "command"), request, map(request.get("arguments")))) return;
            } catch (VMDisconnectedException e) {
                fail(request, "The program has ended.");
            } catch (IncompatibleThreadStateException e) {
                fail(request, "The program is not stopped.");
            } catch (Exception e) {
                fail(request, e.getClass().getSimpleName() + (e.getMessage() == null ? "" : ": " + e.getMessage()));
            }
        }
        shutdown();
    }

    /** @return true when the adapter should exit. */
    private boolean handle(String command, Map<String, Object> req, Map<String, Object> args) throws Exception {
        switch (command == null ? "" : command) {
            case "initialize":
                respond(req, obj("supportsConfigurationDoneRequest", true, "supportsTerminateRequest", true,
                        "supportsEvaluateForHovers", true, "supportsConditionalBreakpoints", false));
                break;
            case "launch": launch(req, args); break;
            case "setBreakpoints": setBreakpoints(req, args); break;
            case "setExceptionBreakpoints": respond(req, obj("breakpoints", new ArrayList<>())); break;
            case "configurationDone":
                respond(req, obj());
                releaseStart();
                break;
            case "threads": threads(req); break;
            case "stackTrace": stackTrace(req, args); break;
            case "scopes": scopes(req, args); break;
            case "variables": variables(req, args); break;
            case "evaluate": evaluate(req, args); break;
            case "continue":
                respond(req, obj("allThreadsContinued", true));
                resume();
                break;
            case "next": step(req, args, StepRequest.STEP_OVER); break;
            case "stepIn": step(req, args, StepRequest.STEP_INTO); break;
            case "stepOut": step(req, args, StepRequest.STEP_OUT); break;
            case "pause": pause(req); break;
            case "terminate":
                respond(req, obj());
                if (process != null) process.destroyForcibly();
                break;
            case "disconnect":
                ending = true;
                respond(req, obj());
                shutdown();
                return true;
            default:
                fail(req, "Unsupported request: " + command);
        }
        return false;
    }

    /** Starts the program: resumes the held VMStartEvent set (or, when it has not arrived yet, lets it through when it does). */
    private void releaseStart() {
        synchronized (startGate) {
            configured = true;
            if (heldStart != null) { heldStart.resume(); heldStart = null; }
            else vm.resume();
        }
    }

    private void shutdown() {
        ending = true;
        try { if (vm != null) vm.dispose(); } catch (RuntimeException ignored) { }
        if (process != null) process.destroyForcibly();
    }

    // ------------------------------------------------------------------ launch

    private void launch(Map<String, Object> req, Map<String, Object> args) throws Exception {
        String mainClass = str(args, "mainClass");
        if (mainClass == null || mainClass.isEmpty()) { fail(req, "launch: mainClass is required"); return; }
        String classpath = str(args, "classpath");
        String cwd = str(args, "cwd");
        List<String> cmd = new ArrayList<>();
        cmd.add(Path.of(System.getProperty("java.home"), "bin", "java").toString());
        // Loopback only; port 0 = any free port, announced on the debuggee's stdout.
        cmd.add("-agentlib:jdwp=transport=dt_socket,server=y,suspend=y,address=127.0.0.1:0");
        for (Object o : list(args.get("vmArgs"))) cmd.add(String.valueOf(o));
        if (classpath != null && !classpath.isEmpty()) { cmd.add("-cp"); cmd.add(classpath); }
        cmd.add(mainClass);
        for (Object o : list(args.get("args"))) cmd.add(String.valueOf(o));
        for (Object o : list(args.get("sourcePaths"))) sourceRoots.add(String.valueOf(o));
        if (cwd != null) sourceRoots.add(cwd);

        ProcessBuilder pb = new ProcessBuilder(cmd);
        if (cwd != null) pb.directory(new java.io.File(cwd));
        process = pb.start();
        process.getOutputStream().close();
        pump("stderr", new BufferedReader(new InputStreamReader(process.getErrorStream(), StandardCharsets.UTF_8)));
        BufferedReader stdout = new BufferedReader(new InputStreamReader(process.getInputStream(), StandardCharsets.UTF_8));
        String announce;
        try {
            announce = CompletableFuture.supplyAsync(() -> {
                try { return stdout.readLine(); } catch (IOException e) { return null; }
            }).get(30, TimeUnit.SECONDS);
        } catch (Exception e) {
            announce = null;
        }
        Matcher m = announce == null ? null : LISTENING.matcher(announce);
        if (m == null || !m.find()) {
            process.destroyForcibly();
            fail(req, "The program's JVM did not start" + (announce == null ? "." : ": " + announce));
            return;
        }
        pump("stdout", stdout);

        AttachingConnector connector = null;
        for (AttachingConnector c : Bootstrap.virtualMachineManager().attachingConnectors())
            if (c.name().equals("com.sun.jdi.SocketAttach")) connector = c;
        if (connector == null) { process.destroyForcibly(); fail(req, "The JDK has no socket attaching connector."); return; }
        Map<String, Connector.Argument> ca = connector.defaultArguments();
        ca.get("hostname").setValue("127.0.0.1");
        ca.get("port").setValue(m.group(1));
        vm = connector.attach(ca);

        EventRequestManager erm = vm.eventRequestManager();
        ClassPrepareRequest prepare = erm.createClassPrepareRequest();
        prepare.setSuspendPolicy(EventRequest.SUSPEND_ALL);
        prepare.enable();
        com.sun.jdi.request.ExceptionRequest uncaught = erm.createExceptionRequest(null, false, true);
        uncaught.setSuspendPolicy(EventRequest.SUSPEND_ALL);
        uncaught.enable();

        launched = true;
        Thread events = new Thread(this::eventLoop, "jdi-events");
        events.setDaemon(true);
        events.start();
        respond(req, obj());
        event("initialized", obj());
    }

    private void pump(String category, BufferedReader reader) {
        Thread t = new Thread(() -> {
            char[] buf = new char[4096];
            try {
                int n;
                while ((n = reader.read(buf)) > 0) output(category, new String(buf, 0, n));
            } catch (IOException ignored) { }
        }, "pump-" + category);
        t.setDaemon(true);
        t.start();
        synchronized (pumps) { pumps.add(t); }
    }

    // ------------------------------------------------------------------ breakpoints

    private void setBreakpoints(Map<String, Object> req, Map<String, Object> args) {
        String path = str(map(args.get("source")), "path");
        if (path == null) { fail(req, "setBreakpoints: source.path is required"); return; }
        List<Object> result = new ArrayList<>();
        synchronized (breakpoints) {   // the event thread resolves breakpoints of newly loaded classes
            List<Bp> old = breakpoints.remove(path);
            if (old != null && vm != null) for (Bp b : old) for (BreakpointRequest r : b.requests) vm.eventRequestManager().deleteEventRequest(r);
            List<Bp> fresh = new ArrayList<>();
            for (Object o : list(args.get("breakpoints"))) fresh.add(new Bp(nextBpId++, path, num(map(o), "line", 0)));
            breakpoints.put(path, fresh);
            if (vm != null) for (ReferenceType t : vm.allClasses()) for (Bp b : fresh) resolve(b, t);
            for (Bp b : fresh) result.add(describe(b));
        }
        respond(req, obj("breakpoints", result));
    }

    private Map<String, Object> describe(Bp b) {
        Map<String, Object> m = obj("id", b.id, "verified", b.verified, "line", b.resolvedLine);
        if (!b.verified) m.put("message", "No executable code at this line yet (the class may not be loaded, or the line has no code).");
        return m;
    }

    private boolean sourceMatches(ReferenceType t, String path) {
        try {
            for (String sp : t.sourcePaths(vm.getDefaultStratum())) {
                String rel = sp.replace('\\', '/');
                if (path.equals(rel) || path.endsWith("/" + rel)) return true;
            }
        } catch (AbsentInformationException | ClassNotPreparedException ignored) { }
        return false;
    }

    /** Puts a JDI breakpoint for {@code b} into {@code t} when t holds the file and the line. */
    private void resolve(Bp b, ReferenceType t) {
        if (!sourceMatches(t, b.path)) return;
        try {
            List<Location> locations = t.locationsOfLine(b.line);
            int line = b.line;
            if (locations.isEmpty() && !b.verified && t.name().indexOf('$') < 0) {
                // A blank or comment line: move to the next line of this top-level class that has code.
                int next = Integer.MAX_VALUE;
                for (Location l : t.allLineLocations()) if (l.lineNumber() > b.line && l.lineNumber() < next) next = l.lineNumber();
                if (next != Integer.MAX_VALUE) { line = next; locations = t.locationsOfLine(next); }
            }
            Map<Method, Location> first = new LinkedHashMap<>();   // one stop per method, at the first instruction of the line
            for (Location l : locations) {
                Location prev = first.get(l.method());
                if (prev == null || l.codeIndex() < prev.codeIndex()) first.put(l.method(), l);
            }
            for (Location l : first.values()) {
                BreakpointRequest r = vm.eventRequestManager().createBreakpointRequest(l);
                r.setSuspendPolicy(EventRequest.SUSPEND_ALL);
                r.enable();
                b.requests.add(r);
            }
            if (!first.isEmpty()) {
                boolean wasVerified = b.verified;
                b.verified = true;
                b.resolvedLine = line;
                if (!wasVerified && launched) event("breakpoint", obj("reason", "changed", "breakpoint", describe(b)));
            }
        } catch (AbsentInformationException | ClassNotPreparedException ignored) { }
    }

    // ------------------------------------------------------------------ events

    private void eventLoop() {
        try {
            while (true) {
                EventSet set = vm.eventQueue().remove();
                boolean stopped = false;
                for (Event e : set) {
                    if (e instanceof VMStartEvent) {
                        synchronized (startGate) {
                            if (configured) break;   // the client is ready: fall through to the resume below
                            heldStart = set;
                        }
                        stopped = true;   // not resumed here
                    }
                    else if (e instanceof BreakpointEvent be) { stopped = true; stopAt("breakpoint", null, be.thread()); }
                    else if (e instanceof StepEvent se) {
                        vm.eventRequestManager().deleteEventRequest(se.request());
                        stopped = true;
                        stopAt("step", null, se.thread());
                    } else if (e instanceof ExceptionEvent ee) {
                        stopped = true;
                        stopAt("exception", "Uncaught " + ee.exception().referenceType().name(), ee.thread());
                    } else if (e instanceof ClassPrepareEvent cp) {
                        synchronized (breakpoints) { for (List<Bp> bps : breakpoints.values()) for (Bp b : bps) resolve(b, cp.referenceType()); }
                    } else if (e instanceof VMDeathEvent || e instanceof VMDisconnectEvent) {
                        vmEnded();
                        return;
                    }
                }
                if (!stopped) set.resume();
            }
        } catch (VMDisconnectedException | InterruptedException e) {
            vmEnded();
        }
    }

    private void stopAt(String reason, String text, ThreadReference thread) {
        clearHandles();
        Map<String, Object> body = obj("reason", reason, "threadId", (int) thread.uniqueID(), "allThreadsStopped", true);
        if (text != null) { body.put("description", text); body.put("text", text); }
        event("stopped", body);
    }

    private void vmEnded() {
        synchronized (this) {
            if (terminatedSent) return;
            terminatedSent = true;
        }
        int code = 0;
        try {
            if (process != null) {
                if (!process.waitFor(5, TimeUnit.SECONDS)) process.destroyForcibly().waitFor(2, TimeUnit.SECONDS);
                code = process.exitValue();
            }
            synchronized (pumps) { for (Thread t : pumps) t.join(2000); }   // flush the program's last output before reporting the end
        } catch (InterruptedException | IllegalThreadStateException ignored) { }
        if (!ending) event("exited", obj("exitCode", code));
        event("terminated", obj());
    }

    private void clearHandles() {
        synchronized (handles) { handles.clear(); frames.clear(); }
    }

    private void resume() {
        clearHandles();
        vm.resume();
    }

    private void pause(Map<String, Object> req) {
        respond(req, obj());
        vm.suspend();
        ThreadReference pick = null;
        for (ThreadReference t : vm.allThreads()) if (t.name().equals("main")) pick = t;
        if (pick == null && !vm.allThreads().isEmpty()) pick = vm.allThreads().get(0);
        if (pick != null) stopAt("pause", null, pick);
    }

    private void step(Map<String, Object> req, Map<String, Object> args, int depth) {
        ThreadReference thread = thread(num(args, "threadId", -1));
        if (thread == null) { fail(req, "Unknown thread."); return; }
        EventRequestManager erm = vm.eventRequestManager();
        for (StepRequest old : new ArrayList<>(erm.stepRequests())) if (old.thread().equals(thread)) erm.deleteEventRequest(old);
        StepRequest r = erm.createStepRequest(thread, StepRequest.STEP_LINE, depth);
        r.addCountFilter(1);
        r.setSuspendPolicy(EventRequest.SUSPEND_ALL);
        for (String ex : EXCLUDED) r.addClassExclusionFilter(ex);   // never walk into the JDK
        r.enable();
        respond(req, obj());
        resume();
    }

    // ------------------------------------------------------------------ threads, stack, variables

    private ThreadReference thread(int id) {
        for (ThreadReference t : vm.allThreads()) if (t.uniqueID() == id) return t;
        return null;
    }

    private void threads(Map<String, Object> req) {
        List<Object> result = new ArrayList<>();
        for (ThreadReference t : vm.allThreads())
            if (t.threadGroup() != null && t.threadGroup().name().equals("main")) result.add(obj("id", (int) t.uniqueID(), "name", t.name()));
        respond(req, obj("threads", result));
    }

    private void stackTrace(Map<String, Object> req, Map<String, Object> args) throws IncompatibleThreadStateException {
        ThreadReference t = thread(num(args, "threadId", -1));
        if (t == null) { fail(req, "Unknown thread."); return; }
        int start = num(args, "startFrame", 0), levels = num(args, "levels", 50);
        List<StackFrame> all = t.frames();
        List<Object> result = new ArrayList<>();
        for (int k = start; k < all.size() && result.size() < levels; k++) {
            StackFrame f = all.get(k);
            Location l = f.location();
            int id;
            synchronized (handles) { id = nextHandle++; frames.put(id, f); }
            String type = l.declaringType().name();
            Map<String, Object> frame = obj("id", id, "name", type.substring(type.lastIndexOf('.') + 1) + "." + l.method().name(),
                    "line", Math.max(l.lineNumber(), 0), "column", 1);
            Map<String, Object> source = sourceOf(l);
            if (source != null) frame.put("source", source);
            result.add(frame);
        }
        respond(req, obj("stackFrames", result, "totalFrames", all.size()));
    }

    private Map<String, Object> sourceOf(Location l) {
        try {
            String rel = l.sourcePath().replace('\\', '/');
            for (String root : sourceRoots) {
                Path p = Path.of(root).resolve(rel);
                if (Files.isRegularFile(p)) return obj("name", l.sourceName(), "path", p.toRealPath().toString());
            }
            return obj("name", l.sourceName());
        } catch (AbsentInformationException | IOException e) {
            return null;
        }
    }

    private int handle(Object target) {
        synchronized (handles) {
            int id = nextHandle++;
            handles.put(id, target);
            return id;
        }
    }

    private void scopes(Map<String, Object> req, Map<String, Object> args) {
        StackFrame f;
        synchronized (handles) { f = frames.get(num(args, "frameId", -1)); }
        if (f == null) { fail(req, "The frame is no longer valid."); return; }
        List<Object> scopes = new ArrayList<>();
        scopes.add(obj("name", "Locals", "variablesReference", handle(new Locals(f)), "expensive", false));
        respond(req, obj("scopes", scopes));
    }

    private void variables(Map<String, Object> req, Map<String, Object> args) throws Exception {
        Object target;
        synchronized (handles) { target = handles.get(num(args, "variablesReference", -1)); }
        List<Object> result = new ArrayList<>();
        if (target instanceof Locals l) {
            ObjectReference self = l.frame().thisObject();
            if (self != null) result.add(variable("this", self));
            try {
                List<LocalVariable> vars = l.frame().visibleVariables();
                Map<LocalVariable, Value> values = l.frame().getValues(vars);
                for (LocalVariable v : vars) result.add(variable(v.name(), values.get(v)));
            } catch (AbsentInformationException e) {
                result.add(obj("name", "(no debug information)", "value", "compile with javac -g", "variablesReference", 0));
            }
        } else if (target instanceof Fields f) {
            List<Field> fields = new ArrayList<>();
            for (Field x : f.object().referenceType().visibleFields()) if (!x.isSynthetic()) fields.add(x);
            Map<Field, Value> values = f.object().getValues(fields);
            for (Field x : fields) result.add(variable(x.name(), values.get(x)));
        } else if (target instanceof Elements e) {
            int len = e.array().length(), shown = Math.min(len, 200);
            List<Value> items = e.array().getValues(0, shown);
            for (int k = 0; k < shown; k++) result.add(variable("[" + k + "]", items.get(k)));
            if (len > shown) result.add(obj("name", "…", "value", (len - shown) + " more elements", "variablesReference", 0));
        }
        respond(req, obj("variables", result));
    }

    private Map<String, Object> variable(String name, Value v) {
        Map<String, Object> m = render(v);
        m.put("name", name);
        return m;
    }

    private Map<String, Object> render(Value v) {
        if (v == null) return obj("value", "null", "type", "null", "variablesReference", 0);
        String type = v.type().name();
        if (v instanceof StringReference s) {
            String text = s.value();
            if (text.length() > 1000) text = text.substring(0, 1000) + "…";
            return obj("value", Json.write(text), "type", type, "variablesReference", 0);
        }
        if (v instanceof PrimitiveValue) return obj("value", v.toString(), "type", type, "variablesReference", 0);
        if (v instanceof ArrayReference a) {
            String element = type.substring(0, type.length() - 2);
            return obj("value", element + "[" + a.length() + "] (id=" + a.uniqueID() + ")", "type", type, "variablesReference", a.length() > 0 ? handle(new Elements(a)) : 0);
        }
        if (v instanceof ObjectReference o) {
            if (isBoxed(type)) {
                Field f = o.referenceType().fieldByName("value");
                if (f != null) return obj("value", render(o.getValue(f)).get("value"), "type", type, "variablesReference", 0);
            }
            return obj("value", type.substring(type.lastIndexOf('.') + 1) + " (id=" + o.uniqueID() + ")", "type", type, "variablesReference", handle(new Fields(o)));
        }
        return obj("value", String.valueOf(v), "type", type, "variablesReference", 0);
    }

    private static boolean isBoxed(String type) {
        switch (type) {
            case "java.lang.Integer": case "java.lang.Long": case "java.lang.Short": case "java.lang.Byte":
            case "java.lang.Character": case "java.lang.Boolean": case "java.lang.Float": case "java.lang.Double":
                return true;
            default: return false;
        }
    }

    // ------------------------------------------------------------------ evaluate: variable lookup only

    private static final Pattern PATH = Pattern.compile("\\s*([A-Za-z_$][\\w$]*)((?:\\s*\\.\\s*[A-Za-z_$][\\w$]*|\\s*\\[\\s*\\d+\\s*\\])*)\\s*");
    private static final Pattern STEP = Pattern.compile("\\.\\s*([A-Za-z_$][\\w$]*)|\\[\\s*(\\d+)\\s*\\]");

    private void evaluate(Map<String, Object> req, Map<String, Object> args) throws Exception {
        String expression = str(args, "expression");
        StackFrame f;
        synchronized (handles) { f = frames.get(num(args, "frameId", -1)); }
        Matcher m = expression == null ? null : PATH.matcher(expression);
        if (f == null || m == null || !m.matches()) {
            fail(req, f == null ? "Evaluation needs a stopped program." : "Only variable lookup is supported: name, a.b, a[0].");
            return;
        }
        Value v = lookup(f, m.group(1));
        Matcher st = STEP.matcher(m.group(2));
        while (st.find()) {
            if (st.group(1) != null) {
                String name = st.group(1);
                if (v instanceof ArrayReference a && name.equals("length")) { v = a.virtualMachine().mirrorOf(a.length()); continue; }
                if (!(v instanceof ObjectReference o)) throw new IllegalArgumentException("'" + name + "' is not a field of a value without fields");
                Field fld = o.referenceType().fieldByName(name);
                if (fld == null) throw new IllegalArgumentException("No field '" + name + "' in " + o.referenceType().name());
                v = o.getValue(fld);
            } else {
                if (!(v instanceof ArrayReference a)) throw new IllegalArgumentException("Not an array");
                int idx = Integer.parseInt(st.group(2));
                if (idx >= a.length()) throw new IllegalArgumentException("Index " + idx + " is out of bounds for length " + a.length());
                v = a.getValue(idx);
            }
        }
        Map<String, Object> r = render(v);
        respond(req, obj("result", r.get("value"), "type", r.get("type"), "variablesReference", r.get("variablesReference")));
    }

    private Value lookup(StackFrame f, String name) throws Exception {
        if (name.equals("this") && f.thisObject() != null) return f.thisObject();
        try {
            LocalVariable local = f.visibleVariableByName(name);
            if (local != null) return f.getValue(local);
        } catch (AbsentInformationException ignored) { }
        ObjectReference self = f.thisObject();
        if (self != null) {
            Field fld = self.referenceType().fieldByName(name);
            if (fld != null) return self.getValue(fld);
        }
        Field fld = f.location().declaringType().fieldByName(name);
        if (fld != null && fld.isStatic()) return f.location().declaringType().getValue(fld);
        throw new IllegalArgumentException("Unknown variable '" + name + "'");
    }
}
