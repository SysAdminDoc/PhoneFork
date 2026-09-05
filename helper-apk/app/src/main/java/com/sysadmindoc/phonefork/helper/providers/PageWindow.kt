package com.sysadmindoc.phonefork.helper.providers

/**
 * Pagination arithmetic for the provider export loop (F120).
 *
 * Pulled out of `exportRows` so the offset/limit behaviour can be unit-tested on the JVM without
 * a device, a Cursor or a Context. The provider walks a fresh cursor on every query, skipping
 * `offset` rows and emitting at most `limit`; the only subtle part is deciding whether another
 * page exists, which is what this type answers.
 */
internal data class PageWindow(
    val offset: Int,
    val limit: Int,
) {
    init {
        require(offset >= 0) { "offset must not be negative" }
        require(limit > 0) { "limit must be positive" }
    }

    /**
     * Next offset to request, or null when this page was the last one.
     *
     * A further page exists only when this one filled to [limit] AND the cursor still had at
     * least one row beyond it. Reporting a next offset without that lookahead would hand the
     * host an offset that returns nothing; omitting it when rows remain would silently truncate
     * the export.
     */
    fun nextOffset(emitted: Int, sawRowBeyondPage: Boolean): Int? =
        if (emitted == limit && sawRowBeyondPage) offset + emitted else null

    /** Rows to skip before emitting. */
    fun shouldSkip(rowsSkippedSoFar: Int): Boolean = rowsSkippedSoFar < offset

    /** Whether this page still has room. */
    fun hasRoom(emitted: Int): Boolean = emitted < limit

    companion object {
        /**
         * Clamps caller-supplied query parameters. A hostile or buggy caller must not be able to
         * ask for an unbounded page or a negative offset.
         */
        fun of(offset: Int, limit: Int, defaultLimit: Int, maxLimit: Int): PageWindow =
            PageWindow(
                offset = offset.coerceAtLeast(0),
                limit = (if (limit <= 0) defaultLimit else limit).coerceIn(1, maxLimit),
            )
    }
}
