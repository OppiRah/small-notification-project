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

    fun notificationPosted(notification: NormalizedNotification): String {
        val payload = JSONObject().apply {
            put("notificationId", notification.key)
            put("packageName", notification.packageName)
            put("appName", notification.appName)
            put("title", notification.title)
            put("body", notification.bigText ?: notification.body)
            put("expandedLines", JSONArray(notification.expandedLines))
            put("summary", notification.summary)
            put("timestamp", isoTimestamp(notification.timestamp))
            put("category", notification.category)
        }
        return envelope("NOTIFICATION", payload)
    }

    fun notificationRemoved(key: String): String {
        val payload = JSONObject().apply {
            put("notificationId", key)
        }
        return envelope("NOTIFICATION_REMOVED", payload)
    }

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
