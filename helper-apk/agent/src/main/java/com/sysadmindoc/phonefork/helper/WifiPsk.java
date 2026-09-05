package com.sysadmindoc.phonefork.helper;

import java.lang.reflect.Method;
import java.util.ArrayList;
import java.util.List;

/**
 * Saved Wi-Fi network export, including pre-shared keys (F116).
 *
 * <p>Android 11 granted the shell user the permission behind
 * {@code WifiManager.getPrivilegedConfiguredNetworks()}, which is what lets an ADB-driven process
 * read saved PSKs with no root and no Shizuku installed. That method is {@code @SystemApi @hide},
 * so it is not on the public {@code android.jar} surface and has to be reached reflectively
 * through {@code IWifiManager}, whose signature has changed across releases.
 *
 * <p>Every candidate signature is tried in turn and the first that answers wins. When none does,
 * the agent reports a structured error rather than a stack trace so the host can fall back to the
 * SSID-only path with a reason to show the user.
 *
 * <p>This class deliberately performs no logging: the values it handles are secrets, and stdout is
 * the only channel they should ever travel on.
 */
final class WifiPsk {

    private WifiPsk() {
    }

    static String export() {
        Object wifiService;
        try {
            wifiService = wifiManagerBinder();
        } catch (Throwable t) {
            return Agent.error("wifi-service-unavailable",
                    "Could not reach the wifi system service: " + t);
        }

        List<?> configurations = null;
        String lastFailure = "no candidate signature matched";
        for (Method method : wifiService.getClass().getMethods()) {
            if (!"getPrivilegedConfiguredNetworks".equals(method.getName())) {
                continue;
            }
            try {
                Object result = method.invoke(wifiService, argumentsFor(method));
                configurations = unwrapList(result);
                if (configurations != null) {
                    break;
                }
            } catch (Throwable t) {
                lastFailure = method.getParameterCount() + "-arg overload: " + t;
            }
        }

        if (configurations == null) {
            return Agent.error("privileged-networks-unavailable",
                    "getPrivilegedConfiguredNetworks() did not answer (" + lastFailure + "). "
                            + "The shell user needs Android 11 or later for this path.");
        }

        StringBuilder items = new StringBuilder("[");
        int count = 0;
        for (Object configuration : configurations) {
            String row = describe(configuration);
            if (row == null) {
                continue;
            }
            if (count > 0) {
                items.append(',');
            }
            items.append(row);
            count++;
        }
        items.append(']');

        return Agent.envelope("ok", "export", count, items.toString(), null);
    }

    /** Reaches the wifi binder without a Context, which app_process does not have. */
    private static Object wifiManagerBinder() throws Exception {
        Class<?> serviceManager = Class.forName("android.os.ServiceManager");
        Method getService = serviceManager.getMethod("getService", String.class);
        Object binder = getService.invoke(null, "wifi");
        if (binder == null) {
            throw new IllegalStateException("ServiceManager returned no \"wifi\" binder.");
        }
        Class<?> stub = Class.forName("android.net.wifi.IWifiManager$Stub");
        Method asInterface = stub.getMethod("asInterface", Class.forName("android.os.IBinder"));
        Object service = asInterface.invoke(null, binder);
        if (service == null) {
            throw new IllegalStateException("IWifiManager.asInterface returned null.");
        }
        return service;
    }

    /**
     * Fills a candidate overload's parameters. Across releases the method has taken zero args,
     * a calling package, a package plus attribution tag, and a trailing extras Bundle.
     */
    private static Object[] argumentsFor(Method method) {
        Class<?>[] types = method.getParameterTypes();
        Object[] args = new Object[types.length];
        for (int i = 0; i < types.length; i++) {
            if (types[i] == String.class) {
                // Shell's package name; the framework attributes the call to it.
                args[i] = i == 0 ? "com.android.shell" : null;
            } else if (android.os.Bundle.class.isAssignableFrom(types[i])) {
                args[i] = new android.os.Bundle();
            } else {
                args[i] = null;
            }
        }
        return args;
    }

    /** Unwraps either a bare List or a ParceledListSlice. */
    private static List<?> unwrapList(Object result) throws Exception {
        if (result == null) {
            return null;
        }
        if (result instanceof List) {
            return (List<?>) result;
        }
        Method getList = result.getClass().getMethod("getList");
        Object list = getList.invoke(result);
        return list instanceof List ? (List<?>) list : null;
    }

    /**
     * Reads the fields PhoneFork needs off a {@code WifiConfiguration}. Field access is
     * reflective so a missing field on some OEM build degrades that one row rather than the
     * whole export.
     */
    private static String describe(Object configuration) {
        if (configuration == null) {
            return null;
        }
        String ssid = stripQuotes(readString(configuration, "SSID"));
        if (ssid == null || ssid.isEmpty()) {
            return null;
        }

        String psk = stripQuotes(readString(configuration, "preSharedKey"));
        Object hiddenSsid = readField(configuration, "hiddenSSID");
        boolean hidden = hiddenSsid instanceof Boolean && (Boolean) hiddenSsid;

        StringBuilder sb = new StringBuilder();
        sb.append("{\"ssid\":\"").append(Agent.escape(ssid)).append('"');
        sb.append(",\"hidden\":").append(hidden);
        sb.append(",\"auth\":\"").append(Agent.escape(authOf(configuration, psk))).append('"');
        if (psk != null && !psk.isEmpty()) {
            sb.append(",\"psk\":\"").append(Agent.escape(psk)).append('"');
        }
        sb.append('}');
        return sb.toString();
    }

    /**
     * Best-effort security type. WifiConfiguration exposes allowedKeyManagement as a BitSet whose
     * indices are stable: 0 NONE, 1 WPA_PSK, 2 WPA_EAP, 3 IEEE8021X, 8 SAE.
     */
    private static String authOf(Object configuration, String psk) {
        Object keyManagement = readField(configuration, "allowedKeyManagement");
        if (keyManagement instanceof java.util.BitSet) {
            java.util.BitSet bits = (java.util.BitSet) keyManagement;
            if (bits.get(8) || bits.get(1)) {
                return "wpa";
            }
            if (bits.get(2) || bits.get(3)) {
                return "wpa-eap";
            }
            if (bits.get(0)) {
                return (psk == null || psk.isEmpty()) ? "nopass" : "wpa";
            }
        }
        Object wepKeys = readField(configuration, "wepKeys");
        if (wepKeys instanceof String[]) {
            for (String key : (String[]) wepKeys) {
                if (key != null && !key.isEmpty()) {
                    return "wep";
                }
            }
        }
        return (psk == null || psk.isEmpty()) ? "nopass" : "wpa";
    }

    private static String readString(Object target, String fieldName) {
        Object value = readField(target, fieldName);
        return value instanceof String ? (String) value : null;
    }

    private static Object readField(Object target, String fieldName) {
        try {
            return target.getClass().getField(fieldName).get(target);
        } catch (Throwable t) {
            return null;
        }
    }

    private static String stripQuotes(String value) {
        if (value == null) {
            return null;
        }
        if (value.length() >= 2 && value.charAt(0) == '"' && value.charAt(value.length() - 1) == '"') {
            return value.substring(1, value.length() - 1);
        }
        return value;
    }
}
