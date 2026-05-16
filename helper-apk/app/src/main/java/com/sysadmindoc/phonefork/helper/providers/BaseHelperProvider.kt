package com.sysadmindoc.phonefork.helper.providers

import android.content.ContentProvider
import android.content.ContentValues
import android.database.Cursor
import android.database.MatrixCursor
import android.net.Uri
import android.os.Bundle
import android.os.Process

/**
 * Shared base for every PhoneForkHelper ContentProvider. Centralizes:
 *
 *  - a friendly "this is the helper" health probe at <authority>/health
 *  - a strict UID gate so only the shell UID (driven by ADB from the Windows host)
 *    can query/insert. Anything else returns an empty cursor.
 *  - a JSON envelope helper so each provider emits `MatrixCursor(["json"])` rows.
 *
 *  Bodies for individual categories (SMS, call log, contacts, Wi-Fi, etc.) live
 *  in their dedicated subclasses; this base only encodes the cross-cutting rules.
 */
abstract class BaseHelperProvider : ContentProvider() {

    /** Logical name used in error messages — not exposed to the host. */
    protected abstract val authorityName: String

    /**
     * Allow-list of UIDs that may call into this provider. The shell UID (2000) covers
     * the ADB path; system UID (1000) is included so internal Android framework probes
     * (e.g. media scanner) don't crash if they ever hit our authority. Everything else
     * is refused with an empty cursor so a misinstalled helper can't be abused by
     * a third-party app on the same device.
     */
    private val allowedUids = setOf(Process.SHELL_UID, Process.SYSTEM_UID)

    final override fun onCreate(): Boolean = true

    final override fun getType(uri: Uri): String = "vnd.android.cursor.dir/${authorityName}.json"

    override fun query(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? {
        if (!isCallerAllowed()) return MatrixCursor(arrayOf("json"))

        // Reserved health endpoint: any provider answers /health with a one-row JSON status.
        if (uri.lastPathSegment == "health") {
            return jsonCursor("""{"authority":"$authorityName","ok":true}""")
        }

        return onQuery(uri, projection, selection, selectionArgs, sortOrder)
    }

    final override fun insert(uri: Uri, values: ContentValues?): Uri? {
        if (!isCallerAllowed()) return null
        return onInsert(uri, values)
    }

    final override fun update(
        uri: Uri,
        values: ContentValues?,
        selection: String?,
        selectionArgs: Array<out String>?
    ): Int {
        if (!isCallerAllowed()) return 0
        return onUpdate(uri, values, selection, selectionArgs)
    }

    final override fun delete(uri: Uri, selection: String?, selectionArgs: Array<out String>?): Int {
        if (!isCallerAllowed()) return 0
        return onDelete(uri, selection, selectionArgs)
    }

    // Subclasses fill these in once the host wire-protocol is finalized.
    protected open fun onQuery(
        uri: Uri,
        projection: Array<out String>?,
        selection: String?,
        selectionArgs: Array<out String>?,
        sortOrder: String?
    ): Cursor? = jsonCursor("""{"authority":"$authorityName","status":"not-implemented"}""")

    protected open fun onInsert(uri: Uri, values: ContentValues?): Uri? = null
    protected open fun onUpdate(uri: Uri, values: ContentValues?, selection: String?, selectionArgs: Array<out String>?): Int = 0
    protected open fun onDelete(uri: Uri, selection: String?, selectionArgs: Array<out String>?): Int = 0

    protected fun jsonCursor(jsonRow: String): Cursor {
        val cursor = MatrixCursor(arrayOf("json"))
        cursor.addRow(arrayOf(jsonRow))
        return cursor
    }

    private fun isCallerAllowed(): Boolean = callingUidOrSelf() in allowedUids

    private fun callingUidOrSelf(): Int = try {
        Binder_callingUid()
    } catch (_: Throwable) {
        Process.myUid()
    }

    // Kept as a wrapper so it is replaceable in unit tests without pulling android.os.Binder.
    @Suppress("FunctionName")
    private fun Binder_callingUid(): Int = android.os.Binder.getCallingUid()

    companion object {
        const val EXTRA_REQUEST_JSON = "phonefork.request.json"
        const val EXTRA_RESPONSE_JSON = "phonefork.response.json"

        @Suppress("unused")
        fun jsonBundle(json: String): Bundle = Bundle().apply { putString(EXTRA_RESPONSE_JSON, json) }
    }
}
