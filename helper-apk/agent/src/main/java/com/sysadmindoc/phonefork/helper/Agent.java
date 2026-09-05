package com.sysadmindoc.phonefork.helper;

/**
 * Push-and-run agent for PhoneFork (F011 / F115), following scrcpy's app_process pattern.
 *
 * <p>The host pushes {@code phonefork-agent.jar} to {@code /data/local/tmp} and runs
 * {@code CLASSPATH=<jar> app_process / com.sysadmindoc.phonefork.helper.Agent <request-json>}.
 * The process runs as the shell user (UID 2000), which holds privileges no installed helper
 * APK can obtain, and leaves nothing behind once the JAR is deleted.
 *
 * <p>The single argv element is a JSON request of the shape {@code {"op":"<name>"}}. Output is
 * one line of {@code phonefork.helper.v1} JSON on stdout so the host can parse it with the same
 * contract it uses for the ContentProviders.
 *
 * <p>Written in plain Java with no third-party runtime dependency: the JAR is dexed and run
 * directly by app_process, so anything it references must already exist on the device.
 */
public final class Agent {

    static final String SCHEMA = "phonefork.helper.v1";
    static final String AUTHORITY = "agent";

    private Agent() {
    }

    public static void main(String[] args) {
        String request = args.length > 0 ? args[0] : "";
        String op = extractOp(request);

        try {
            switch (op) {
                case "ping":
                    System.out.println(ping());
                    break;
                case "wifi-psk":
                    System.out.println(WifiPsk.export());
                    break;
                case "":
                    System.out.println(error("missing-op", "Request must be JSON containing an \"op\" field."));
                    break;
                default:
                    System.out.println(error("unsupported-op", "Unsupported agent op: " + op));
                    break;
            }
        } catch (Throwable t) {
            // app_process prints a stack trace to stderr on an escaped throwable, which the host
            // cannot parse. Always answer with a well-formed envelope instead.
            System.out.println(error("agent-error", String.valueOf(t.getMessage())));
        }
        System.out.flush();
    }

    /**
     * Liveness probe. Reports the UID the agent is running as so the host can confirm it really
     * reached the shell user rather than something less privileged.
     */
    static String ping() {
        StringBuilder item = new StringBuilder();
        item.append("{\"uid\":").append(android.os.Process.myUid());
        item.append(",\"pid\":").append(android.os.Process.myPid());
        item.append(",\"sdkInt\":").append(android.os.Build.VERSION.SDK_INT);
        item.append(",\"isShellUid\":").append(android.os.Process.myUid() == android.os.Process.SHELL_UID);
        item.append("}");

        return envelope("ok", "health", 1, "[" + item + "]", null);
    }

    /**
     * Minimal JSON field read for the one field the agent needs. A full parser would be dead
     * weight in a dexed payload that only ever receives host-generated requests.
     */
    static String extractOp(String requestJson) {
        if (requestJson == null) {
            return "";
        }
        int key = requestJson.indexOf("\"op\"");
        if (key < 0) {
            return "";
        }
        int colon = requestJson.indexOf(':', key);
        if (colon < 0) {
            return "";
        }
        int open = requestJson.indexOf('"', colon);
        if (open < 0) {
            return "";
        }
        int close = requestJson.indexOf('"', open + 1);
        if (close < 0) {
            return "";
        }
        return requestJson.substring(open + 1, close);
    }

    static String error(String code, String message) {
        String err = "{\"code\":\"" + escape(code) + "\",\"message\":\"" + escape(message) + "\"}";
        return envelope("error", "health", 0, "[]", err);
    }

    static String envelope(String status, String mode, int count, String itemsJson, String errorJson) {
        StringBuilder sb = new StringBuilder();
        sb.append("{\"schema\":\"").append(SCHEMA).append("\"");
        sb.append(",\"authority\":\"").append(AUTHORITY).append("\"");
        sb.append(",\"status\":\"").append(status).append("\"");
        sb.append(",\"mode\":\"").append(mode).append("\"");
        sb.append(",\"count\":").append(count);
        sb.append(",\"items\":").append(itemsJson);
        sb.append(",\"capabilities\":{}");
        sb.append(",\"warnings\":[]");
        if (errorJson != null) {
            sb.append(",\"error\":").append(errorJson);
        }
        sb.append("}");
        return sb.toString();
    }

    static String escape(String value) {
        if (value == null) {
            return "";
        }
        StringBuilder sb = new StringBuilder(value.length());
        for (int i = 0; i < value.length(); i++) {
            char c = value.charAt(i);
            switch (c) {
                case '"': sb.append("\\\""); break;
                case '\\': sb.append("\\\\"); break;
                case '\n': sb.append("\\n"); break;
                case '\r': sb.append("\\r"); break;
                case '\t': sb.append("\\t"); break;
                default:
                    if (c < 0x20) {
                        sb.append(String.format("\\u%04x", (int) c));
                    } else {
                        sb.append(c);
                    }
            }
        }
        return sb.toString();
    }
}
