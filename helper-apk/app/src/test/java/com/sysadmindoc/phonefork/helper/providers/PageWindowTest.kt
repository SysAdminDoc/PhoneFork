package com.sysadmindoc.phonefork.helper.providers

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/**
 * F120 — paging must return every row exactly once across pages, with no gap at a page boundary
 * and no repeat. The provider walks a fresh cursor per query, so the arithmetic here is the only
 * thing standing between a caller and a silently truncated export.
 */
class PageWindowTest {

    /**
     * Replays what exportRows does against a cursor of [totalRows]: skip `offset`, emit up to
     * `limit`, and note whether one more row existed beyond the page.
     */
    private fun page(totalRows: Int, offset: Int, limit: Int): Pair<List<Int>, Int?> {
        val window = PageWindow(offset, limit)
        val emitted = mutableListOf<Int>()
        var skipped = 0
        var sawRowBeyondPage = false

        for (row in 0 until totalRows) {
            if (window.shouldSkip(skipped)) {
                skipped++
                continue
            }
            if (window.hasRoom(emitted.size)) {
                emitted.add(row)
            } else {
                sawRowBeyondPage = true
                break
            }
        }
        return emitted to window.nextOffset(emitted.size, sawRowBeyondPage)
    }

    /** Walks every page the provider would hand out, following nextOffset to completion. */
    private fun readAll(totalRows: Int, limit: Int): List<Int> {
        val seen = mutableListOf<Int>()
        var offset: Int? = 0
        var pages = 0
        while (offset != null) {
            val (rows, next) = page(totalRows, offset, limit)
            seen += rows
            offset = next
            pages++
            assertTrue("runaway paging at page $pages", pages <= 100)
        }
        return seen
    }

    @Test
    fun `1001 rows at limit 500 come back complete and in order`() {
        val seen = readAll(totalRows = 1001, limit = 500)

        assertEquals(1001, seen.size)
        assertEquals(1001, seen.distinct().size)
        assertEquals((0 until 1001).toList(), seen)
    }

    @Test
    fun `an exact multiple of the page size does not report a phantom next page`() {
        // 1000 rows at limit 500 fills page two exactly; a lookahead that counted a row it never
        // saw would hand out offset 1000 and return an empty third page.
        val (rows, next) = page(totalRows = 1000, offset = 500, limit = 500)

        assertEquals(500, rows.size)
        assertNull(next)
        assertEquals((0 until 1000).toList(), readAll(totalRows = 1000, limit = 500))
    }

    @Test
    fun `a full page with exactly one row behind it reports that row`() {
        val (rows, next) = page(totalRows = 501, offset = 0, limit = 500)

        assertEquals(500, rows.size)
        assertEquals(500, next)
        assertEquals((0 until 501).toList(), readAll(totalRows = 501, limit = 500))
    }

    @Test
    fun `a short first page is the last page`() {
        val (rows, next) = page(totalRows = 3, offset = 0, limit = 500)

        assertEquals(listOf(0, 1, 2), rows)
        assertNull(next)
    }

    @Test
    fun `an empty result pages once and stops`() {
        val (rows, next) = page(totalRows = 0, offset = 0, limit = 500)

        assertTrue(rows.isEmpty())
        assertNull(next)
    }

    @Test
    fun `an offset past the end returns nothing and stops`() {
        val (rows, next) = page(totalRows = 10, offset = 100, limit = 500)

        assertTrue(rows.isEmpty())
        assertNull(next)
    }

    @Test
    fun `small page sizes still cover every row`() {
        for (limit in 1..7) {
            val seen = readAll(totalRows = 20, limit = limit)
            assertEquals("limit=$limit", (0 until 20).toList(), seen)
        }
    }

    @Test
    fun `caller supplied parameters are clamped`() {
        val negative = PageWindow.of(offset = -5, limit = -1, defaultLimit = 500, maxLimit = 2000)
        assertEquals(0, negative.offset)
        assertEquals(500, negative.limit)

        val huge = PageWindow.of(offset = 10, limit = 999_999, defaultLimit = 500, maxLimit = 2000)
        assertEquals(2000, huge.limit)
        assertEquals(10, huge.offset)
    }

    @Test
    fun `a partial page never reports a next offset`() {
        // Even if the walker claims it saw a row beyond the page, an unfilled page is terminal.
        assertNull(PageWindow(offset = 0, limit = 500).nextOffset(emitted = 499, sawRowBeyondPage = true))
    }
}
