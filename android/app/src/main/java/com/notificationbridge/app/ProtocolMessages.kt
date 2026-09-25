package com.notificationbridge.app

import org.json.JSONArray
import org.json.JSONObject
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.UUID

// Builds wire messages matching the envelope/payload shape defined in PROTOCOL.md.
object ProtocolMessages {

    private const val PROTOCOL_VERSION = 1

    // Mirror the PC's ProtocolLimits (PROTOCOL.md section 6). The PC rejects a whole notification
    // that exceeds any of them, so long text is clipped here instead of being lost entirely.
    private const val APP_NAME_MAX = 256
    private const val TITLE_MAX = 1024
    private const val BODY_MAX = 8192
    private const val EXPANDED_LINES_MAX = 100
    private const val EXPANDED_LINE_MAX = 4096

    fun notificationPosted(notification: NormalizedNotification, iconPngBase64: String? = null): String {
        val payload = JSONObject().apply {
            put("notificationId", notification.key)
            put("packageName", notification.packageName)
            put("appName", clip(notification.appName, APP_NAME_MAX))
            put("title", clip(notification.title, TITLE_MAX))
            put("body", clip(notification.bigText ?: notification.body, BODY_MAX))
            put("expandedLines", JSONArray(notification.expandedLines.take(EXPANDED_LINES_MAX).map { clip(it, EXPANDED_LINE_MAX) }))
            put("summary", notification.summary)
            put("timestamp", isoTimestamp(notification.timestamp))
            put("category", notification.category)
            iconPngBase64?.let { put("iconPng", it) }
        }
        return envelope("NOTIFICATION", payload)
    }

    fun notificationRemoved(key: String): String {
        val payload = JSONObject().apply {
            put("notificationId", key)
        }
        return envelope("NOTIFICATION_REMOVED", payload)
    }

    fun pairRequest(deviceId: String, deviceName: String, proofBase64: String): String {
        val payload = JSONObject().apply {
            put("deviceId", deviceId)
            put("deviceName", deviceName)
            put("proof", proofBase64)
        }
        return envelope("PAIR_REQUEST", payload)
    }

    fun authenticate(deviceId: String, nonce: String, timestamp: String, proofBase64: String): String {
        val payload = JSONObject().apply {
            put("deviceId", deviceId)
            put("nonce", nonce)
            put("timestamp", timestamp)
            put("proof", proofBase64)
        }
        return envelope("AUTHENTICATE", payload)
    }

    fun nowIsoTimestamp(): String = isoTimestamp(System.currentTimeMillis())

    private fun clip(text: String?, max: Int): String? =
        if (text == null || text.length <= max) text else text.take(max - 1) + "…"

    private fun envelope(messageType: String, payload: JSONObject): String {
        return JSONObject().apply {
            put("protocolVersion", PROTOCOL_VERSION)
            put("messageType", messageType)
            put("messageId", UUID.randomUUID().toString())
            put("timestamp", isoTimestamp(System.currentTimeMillis()))
            put("payload", payload)
        }.toString()
    }

    private fun isoTimestamp(millis: Long): String {
        val format = SimpleDateFormat("yyyy-MM-dd'T'HH:mm:ss'Z'", Locale.US)
        format.timeZone = TimeZone.getTimeZone("UTC")
        return format.format(Date(millis))
    }
}
