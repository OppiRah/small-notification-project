package com.notificationbridge.app

import org.json.JSONObject
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.annotation.Config

// The PC rejects an entire notification that exceeds its size limits, so the phone must clip
// long text itself or the notification silently never appears.
@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class ProtocolMessagesTest {

    private fun notification(
        appName: String = "App",
        title: String? = "Title",
        body: String? = "Body",
        bigText: String? = null,
        expandedLines: List<String> = emptyList(),
    ) = NormalizedNotification(
        key = "key-1", packageName = "com.example", appName = appName, title = title, body = body,
        bigText = bigText, expandedLines = expandedLines, summary = null, subText = null,
        timestamp = 1000L, category = null, hasIcon = false, isOngoing = false,
    )

    private fun payloadOf(n: NormalizedNotification): JSONObject =
        JSONObject(ProtocolMessages.notificationPosted(n)).getJSONObject("payload")

    @Test
    fun `text within limits is sent unchanged`() {
        val payload = payloadOf(notification(title = "Jane", body = "Hello"))

        assertEquals("Jane", payload.getString("title"))
        assertEquals("Hello", payload.getString("body"))
    }

    @Test
    fun `icon is sent when provided`() {
        val json = JSONObject(ProtocolMessages.notificationPosted(notification(), "QUJD"))

        assertEquals("QUJD", json.getJSONObject("payload").getString("iconPng"))
    }

    @Test
    fun `icon is omitted when unavailable`() {
        val json = JSONObject(ProtocolMessages.notificationPosted(notification(), null))

        assertTrue(!json.getJSONObject("payload").has("iconPng"))
    }

    @Test
    fun `long body is clipped to the protocol limit`() {
        val payload = payloadOf(notification(bigText = "a".repeat(20_000)))

        val body = payload.getString("body")
        assertEquals(8192, body.length)
        assertTrue(body.endsWith("…"))
    }

    @Test
    fun `long title and app name are clipped to the protocol limits`() {
        val payload = payloadOf(notification(appName = "a".repeat(500), title = "t".repeat(3000)))

        assertEquals(256, payload.getString("appName").length)
        assertEquals(1024, payload.getString("title").length)
    }

    @Test
    fun `too many or too long expanded lines are clipped`() {
        val lines = List(150) { "l".repeat(5000) }
        val payload = payloadOf(notification(expandedLines = lines))

        val sent = payload.getJSONArray("expandedLines")
        assertEquals(100, sent.length())
        assertEquals(4096, sent.getString(0).length)
    }
}
