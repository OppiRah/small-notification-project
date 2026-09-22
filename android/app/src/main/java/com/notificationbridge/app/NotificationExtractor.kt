package com.notificationbridge.app

import android.app.Notification
import android.os.Bundle

// App-agnostic: reads only generic Notification/extras fields, per ADR-003.
object NotificationExtractor {

    fun extract(
        key: String,
        packageName: String,
        appName: String,
        postTimeMillis: Long,
        notification: Notification,
    ): NormalizedNotification {
        val extras = notification.extras

        val expandedLines = mutableListOf<String>()
        extras.getCharSequenceArray(Notification.EXTRA_TEXT_LINES)?.forEach {
            expandedLines.add(it.toString())
        }
        expandedLines.addAll(extractMessagingLines(extras))

        return NormalizedNotification(
            key = key,
            packageName = packageName,
            appName = appName,
            title = extras.getCharSequence(Notification.EXTRA_TITLE)?.toString(),
            body = extras.getCharSequence(Notification.EXTRA_TEXT)?.toString(),
            bigText = extras.getCharSequence(Notification.EXTRA_BIG_TEXT)?.toString(),
            expandedLines = expandedLines,
            summary = extras.getCharSequence(Notification.EXTRA_SUMMARY_TEXT)?.toString(),
            subText = extras.getCharSequence(Notification.EXTRA_SUB_TEXT)?.toString(),
            timestamp = postTimeMillis,
            category = notification.category,
            hasIcon = notification.smallIcon != null,
            isOngoing = (notification.flags and Notification.FLAG_ONGOING_EVENT) != 0,
        )
    }

    @Suppress("DEPRECATION")
    private fun extractMessagingLines(extras: Bundle): List<String> {
        val bundleArray = extras.getParcelableArray(Notification.EXTRA_MESSAGES) ?: return emptyList()
        val messages = Notification.MessagingStyle.Message.getMessagesFromBundleArray(bundleArray)
        return messages.map { message ->
            val sender = message.senderPerson?.name?.toString()
            if (sender != null) "$sender: ${message.text}" else message.text.toString()
        }
    }
}
