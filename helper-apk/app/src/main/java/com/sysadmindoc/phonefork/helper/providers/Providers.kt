package com.sysadmindoc.phonefork.helper.providers

import android.app.WallpaperManager
import android.content.ContentValues
import android.database.Cursor
import android.media.RingtoneManager
import android.net.Uri
import android.net.wifi.WifiManager
import android.provider.CallLog
import android.provider.ContactsContract
import android.provider.Telephony
import android.provider.UserDictionary
import org.json.JSONArray
import org.json.JSONObject

private const val CONTRACT_SCHEMA = "phonefork.helper.v1"
private const val DEFAULT_LIMIT = 500
private const val MAX_LIMIT = 2_000

/**
 * One concrete ContentProvider per migration category. Each provider returns a
 * single `json` column with the same versioned envelope:
 *
 * {
 *   "schema": "phonefork.helper.v1",
 *   "authority": "sms",
 *   "status": "ok|error|unsupported",
 *   "mode": "export|restore|capability|health",
 *   "count": 0,
 *   "nextOffset": null,
 *   "items": [],
 *   "capabilities": {},
 *   "warnings": [],
 *   "error": {"code": "...", "message": "..."}
 * }
 */

class SmsProvider : BaseHelperProvider() {
    override val authorityName = "sms"

    override fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? = exportRows(
        uri = uri,
        target = Telephony.Sms.CONTENT_URI,
        projection = arrayOf(
            Telephony.Sms._ID,
            Telephony.Sms.THREAD_ID,
            Telephony.Sms.ADDRESS,
            Telephony.Sms.DATE,
            Telephony.Sms.READ,
            Telephony.Sms.TYPE,
            Telephony.Sms.STATUS,
            Telephony.Sms.BODY
        ),
        sortOrder = "${Telephony.Sms.DATE} DESC",
        capabilities = capabilities(
            "canRead" to true,
            "canRestore" to false,
            "restoreRequiresDefaultSmsApp" to true
        )
    ) { c ->
        JSONObject()
            .putLong(c, "id", Telephony.Sms._ID)
            .putLong(c, "threadId", Telephony.Sms.THREAD_ID)
            .putStringOrNull(c, "address", Telephony.Sms.ADDRESS)
            .putLong(c, "date", Telephony.Sms.DATE)
            .putInt(c, "read", Telephony.Sms.READ)
            .putInt(c, "type", Telephony.Sms.TYPE)
            .putInt(c, "statusValue", Telephony.Sms.STATUS)
            .putStringOrNull(c, "body", Telephony.Sms.BODY)
    }

    override fun onInsert(uri: Uri, values: ContentValues?): Uri? = rejectRestoreWithoutConfirmation(uri, values)
}

class CallLogProvider : BaseHelperProvider() {
    override val authorityName = "calllog"

    override fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? = exportRows(
        uri = uri,
        target = CallLog.Calls.CONTENT_URI,
        projection = arrayOf(
            CallLog.Calls._ID,
            CallLog.Calls.NUMBER,
            CallLog.Calls.CACHED_NAME,
            CallLog.Calls.TYPE,
            CallLog.Calls.DATE,
            CallLog.Calls.DURATION,
            CallLog.Calls.NEW,
            CallLog.Calls.PHONE_ACCOUNT_COMPONENT_NAME,
            CallLog.Calls.PHONE_ACCOUNT_ID
        ),
        sortOrder = "${CallLog.Calls.DATE} DESC",
        capabilities = capabilities("canRead" to true, "canRestore" to false)
    ) { c ->
        JSONObject()
            .putLong(c, "id", CallLog.Calls._ID)
            .putStringOrNull(c, "number", CallLog.Calls.NUMBER)
            .putStringOrNull(c, "cachedName", CallLog.Calls.CACHED_NAME)
            .putInt(c, "type", CallLog.Calls.TYPE)
            .putLong(c, "date", CallLog.Calls.DATE)
            .putLong(c, "durationSeconds", CallLog.Calls.DURATION)
            .putInt(c, "new", CallLog.Calls.NEW)
            .putStringOrNull(c, "phoneAccountComponent", CallLog.Calls.PHONE_ACCOUNT_COMPONENT_NAME)
            .putStringOrNull(c, "phoneAccountId", CallLog.Calls.PHONE_ACCOUNT_ID)
    }

    override fun onInsert(uri: Uri, values: ContentValues?): Uri? = rejectRestoreWithoutConfirmation(uri, values)
}

class ContactsProvider : BaseHelperProvider() {
    override val authorityName = "contacts"

    override fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? = exportRows(
        uri = uri,
        target = ContactsContract.Contacts.CONTENT_URI,
        projection = arrayOf(
            ContactsContract.Contacts._ID,
            ContactsContract.Contacts.LOOKUP_KEY,
            ContactsContract.Contacts.DISPLAY_NAME_PRIMARY,
            ContactsContract.Contacts.HAS_PHONE_NUMBER,
            ContactsContract.Contacts.STARRED,
            ContactsContract.Contacts.CONTACT_LAST_UPDATED_TIMESTAMP
        ),
        sortOrder = "${ContactsContract.Contacts.DISPLAY_NAME_PRIMARY} COLLATE LOCALIZED ASC",
        capabilities = capabilities(
            "canRead" to true,
            "canRestore" to false,
            "exportsSummaryRows" to true
        )
    ) { c ->
        JSONObject()
            .putLong(c, "id", ContactsContract.Contacts._ID)
            .putStringOrNull(c, "lookupKey", ContactsContract.Contacts.LOOKUP_KEY)
            .putStringOrNull(c, "displayName", ContactsContract.Contacts.DISPLAY_NAME_PRIMARY)
            .putInt(c, "hasPhoneNumber", ContactsContract.Contacts.HAS_PHONE_NUMBER)
            .putInt(c, "starred", ContactsContract.Contacts.STARRED)
            .putLong(c, "lastUpdated", ContactsContract.Contacts.CONTACT_LAST_UPDATED_TIMESTAMP)
    }

    override fun onInsert(uri: Uri, values: ContentValues?): Uri? = rejectRestoreWithoutConfirmation(uri, values)
}

class WifiProvider : BaseHelperProvider() {
    override val authorityName = "wifi"

    override fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? {
        return try {
            val ctx = context ?: return error("context-unavailable", "Provider context is unavailable.")
            val manager = ctx.getSystemService(WifiManager::class.java)
            @Suppress("DEPRECATION")
            val info = manager?.connectionInfo
            val item = JSONObject()
                .put("ssid", info?.ssid?.stripWifiQuotes())
                .put("bssid", info?.bssid)
                .put("networkId", info?.networkId ?: -1)
                .put("rssi", info?.rssi ?: 0)
                .put("linkSpeedMbps", info?.linkSpeed ?: 0)
            jsonCursor(
                envelope(
                    status = "ok",
                    mode = "capability",
                    items = JSONArray().put(item),
                    capabilities = capabilities(
                        "canReadCurrentConnection" to true,
                        "canReadSavedSsids" to false,
                        "canReadPsk" to false,
                        "requiresShizukuOrPrivilegedApiForPsk" to true
                    ),
                    warnings = arrayOf("Android does not expose saved Wi-Fi PSKs to a normal helper APK.")
                ).toString()
            )
        } catch (ex: SecurityException) {
            error("permission-denied", ex.message ?: "Wi-Fi access denied.")
        }
    }
}

class WallpaperProvider : BaseHelperProvider() {
    override val authorityName = "wallpaper"

    override fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? {
        return try {
            val ctx = context ?: return error("context-unavailable", "Provider context is unavailable.")
            val manager = WallpaperManager.getInstance(ctx)
            val info = manager.wallpaperInfo
            val item = JSONObject()
                .put("liveWallpaperPackage", info?.packageName)
                .put("liveWallpaperService", info?.serviceName)
                .put("desiredMinimumWidth", manager.desiredMinimumWidth)
                .put("desiredMinimumHeight", manager.desiredMinimumHeight)
            jsonCursor(
                envelope(
                    status = "ok",
                    mode = "capability",
                    items = JSONArray().put(item),
                    capabilities = capabilities(
                        "canReadMetadata" to true,
                        "canExportBitmap" to false,
                        "canRestore" to false
                    ),
                    warnings = arrayOf("Wallpaper image bytes are not exported by the helper contract yet.")
                ).toString()
            )
        } catch (ex: SecurityException) {
            error("permission-denied", ex.message ?: "Wallpaper metadata access denied.")
        }
    }

    override fun onInsert(uri: Uri, values: ContentValues?): Uri? = rejectRestoreWithoutConfirmation(uri, values)
}

class RingtoneProvider : BaseHelperProvider() {
    override val authorityName = "ringtone"

    override fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? {
        return try {
            val ctx = context ?: return error("context-unavailable", "Provider context is unavailable.")
            val items = JSONArray()
                .put(defaultRingtone(ctx = ctx, type = RingtoneManager.TYPE_RINGTONE, name = "ringtone"))
                .put(defaultRingtone(ctx = ctx, type = RingtoneManager.TYPE_NOTIFICATION, name = "notification"))
                .put(defaultRingtone(ctx = ctx, type = RingtoneManager.TYPE_ALARM, name = "alarm"))
            jsonCursor(
                envelope(
                    status = "ok",
                    mode = "export",
                    items = items,
                    capabilities = capabilities("canRead" to true, "canRestore" to false)
                ).toString()
            )
        } catch (ex: SecurityException) {
            error("permission-denied", ex.message ?: "Ringtone access denied.")
        }
    }

    override fun onInsert(uri: Uri, values: ContentValues?): Uri? = rejectRestoreWithoutConfirmation(uri, values)
}

class DictionaryProvider : BaseHelperProvider() {
    override val authorityName = "dictionary"

    override fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? = exportRows(
        uri = uri,
        target = UserDictionary.Words.CONTENT_URI,
        projection = arrayOf(
            UserDictionary.Words._ID,
            UserDictionary.Words.WORD,
            UserDictionary.Words.FREQUENCY,
            UserDictionary.Words.LOCALE,
            UserDictionary.Words.SHORTCUT
        ),
        sortOrder = "${UserDictionary.Words.WORD} COLLATE LOCALIZED ASC",
        capabilities = capabilities("canRead" to true, "canRestore" to false)
    ) { c ->
        JSONObject()
            .putLong(c, "id", UserDictionary.Words._ID)
            .putStringOrNull(c, "word", UserDictionary.Words.WORD)
            .putInt(c, "frequency", UserDictionary.Words.FREQUENCY)
            .putStringOrNull(c, "locale", UserDictionary.Words.LOCALE)
            .putStringOrNull(c, "shortcut", UserDictionary.Words.SHORTCUT)
    }

    override fun onInsert(uri: Uri, values: ContentValues?): Uri? = rejectRestoreWithoutConfirmation(uri, values)
}

private fun BaseHelperProvider.exportRows(
    uri: Uri,
    target: Uri,
    projection: Array<String>,
    sortOrder: String,
    capabilities: JSONObject,
    mapper: (Cursor) -> JSONObject
): Cursor? {
    if (uri.pathSegments.contains("restore")) return restoreDisabled()

    val limit = uri.queryInt("limit", DEFAULT_LIMIT).coerceIn(1, MAX_LIMIT)
    val offset = uri.queryInt("offset", 0).coerceAtLeast(0)
    return try {
        val ctx = context ?: return error("context-unavailable", "Provider context is unavailable.")
        val cursor = ctx.contentResolver.query(target, projection, null, null, sortOrder)
            ?: return error("query-failed", "Android returned no cursor for $authorityName.")
        cursor.use { c ->
            val items = JSONArray()
            var skipped = 0
            var totalSeen = 0
            while (c.moveToNext()) {
                if (skipped < offset) {
                    skipped++
                    totalSeen++
                    continue
                }
                if (items.length() < limit) items.put(mapper(c))
                totalSeen++
                if (items.length() >= limit && c.moveToNext()) {
                    totalSeen++
                    break
                }
            }
            val nextOffset = if (items.length() == limit && totalSeen > offset + items.length()) {
                offset + items.length()
            } else {
                null
            }
            jsonCursor(
                envelope(
                    status = "ok",
                    mode = "export",
                    items = items,
                    nextOffset = nextOffset,
                    capabilities = capabilities
                ).toString()
            )
        }
    } catch (ex: SecurityException) {
        error("permission-denied", ex.message ?: "Permission denied for $authorityName.")
    } catch (ex: Throwable) {
        error("query-error", ex.message ?: "Unexpected provider failure.")
    }
}

private fun BaseHelperProvider.rejectRestoreWithoutConfirmation(uri: Uri, values: ContentValues?): Uri? {
    if (!uri.pathSegments.contains("restore")) return null
    if (values?.getAsBoolean("confirmRestore") != true &&
        values?.getAsString("confirmRestore")?.equals("true", ignoreCase = true) != true
    ) {
        return null
    }

    return null
}

private fun BaseHelperProvider.restoreDisabled(): Cursor = error(
    code = "restore-disabled",
    message = "Restore requires explicit per-category host confirmation and is not enabled in this helper build.",
    mode = "restore"
)

private fun BaseHelperProvider.error(code: String, message: String, mode: String = "export"): Cursor {
    return jsonCursor(
        envelope(
            status = "error",
            mode = mode,
            error = JSONObject().put("code", code).put("message", message),
            capabilities = capabilities("canRead" to false)
        ).toString()
    )
}

private fun BaseHelperProvider.envelope(
    status: String,
    mode: String,
    items: JSONArray = JSONArray(),
    nextOffset: Int? = null,
    capabilities: JSONObject = JSONObject(),
    warnings: Array<String> = emptyArray(),
    error: JSONObject? = null
): JSONObject {
    val obj = JSONObject()
        .put("schema", CONTRACT_SCHEMA)
        .put("authority", authorityName)
        .put("status", status)
        .put("mode", mode)
        .put("count", items.length())
        .put("items", items)
        .put("capabilities", capabilities)
        .put("warnings", JSONArray().also { arr -> warnings.forEach { arr.put(it) } })
    if (nextOffset != null) obj.put("nextOffset", nextOffset)
    if (error != null) obj.put("error", error)
    return obj
}

private fun capabilities(vararg entries: Pair<String, Any>): JSONObject {
    val obj = JSONObject()
    entries.forEach { (key, value) -> obj.put(key, value) }
    return obj
}

private fun Uri.queryInt(name: String, fallback: Int): Int =
    getQueryParameter(name)?.toIntOrNull() ?: fallback

private fun JSONObject.putStringOrNull(cursor: Cursor, key: String, column: String): JSONObject {
    val index = cursor.getColumnIndex(column)
    return if (index < 0 || cursor.isNull(index)) put(key, JSONObject.NULL) else put(key, cursor.getString(index))
}

private fun JSONObject.putInt(cursor: Cursor, key: String, column: String): JSONObject {
    val index = cursor.getColumnIndex(column)
    return if (index < 0 || cursor.isNull(index)) put(key, JSONObject.NULL) else put(key, cursor.getInt(index))
}

private fun JSONObject.putLong(cursor: Cursor, key: String, column: String): JSONObject {
    val index = cursor.getColumnIndex(column)
    return if (index < 0 || cursor.isNull(index)) put(key, JSONObject.NULL) else put(key, cursor.getLong(index))
}

private fun String.stripWifiQuotes(): String =
    if (length >= 2 && first() == '"' && last() == '"') substring(1, length - 1) else this

private fun defaultRingtone(ctx: android.content.Context, type: Int, name: String): JSONObject {
    val uri = RingtoneManager.getActualDefaultRingtoneUri(ctx, type)
    val title = uri?.let { RingtoneManager.getRingtone(ctx, it)?.getTitle(ctx) }
    return JSONObject()
        .put("kind", name)
        .put("uri", uri?.toString())
        .put("title", title)
}
