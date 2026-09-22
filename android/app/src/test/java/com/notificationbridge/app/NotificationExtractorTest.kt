package com.notificationbridge.app

import android.app.Notification
import android.app.Person
import org.junit.Assert.assertEquals
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner
import org.robolectric.RuntimeEnvironment
import org.robolectric.annotation.Config

@RunWith(RobolectricTestRunner::class)
@Config(sdk = [34])
class NotificationExtractorTest {

    private fun context() = RuntimeEnvironment.getApplication()

    @Test
    fun `extracts title and text`() {
        val notification = Notification.Builder(context(), "channel")
            .setContentTitle("Jane")
            .setContentText("Hello there")
            .build()

        val result = NotificationExtractor.extract(
            key = "key-1", packageName = "com.example", appName = "Example",
            postTimeMillis = 1000L, notification = notification,
        )

        assertEquals("Jane", result.title)
        assertEquals("Hello there", result.body)
    }

    @Test
    fun `extracts big text style`() {
        val notification = Notification.Builder(context(), "channel")
            .setContentTitle("Jane")
            .setStyle(Notification.BigTextStyle().bigText("A much longer message body here."))
            .build()

        val result = NotificationExtractor.extract(
            key = "key-2", packageName = "com.example", appName = "Example",
            postTimeMillis = 1000L, notification = notification,
        )

        assertEquals("A much longer message body here.", result.bigText)
    }

    @Test
    fun `extracts inbox lines`() {
        val notification = Notification.Builder(context(), "channel")
            .setStyle(
                Notification.InboxStyle()
                    .addLine("first line")
                    .addLine("second line")
            )
            .build()

        val result = NotificationExtractor.extract(
            key = "key-3", packageName = "com.example", appName = "Example",
            postTimeMillis = 1000L, notification = notification,
        )

        assertEquals(listOf("first line", "second line"), result.expandedLines)
    }

    @Test
    fun `extracts messaging style with sender`() {
        val person = Person.Builder().setName("Alex").build()
        val notification = Notification.Builder(context(), "channel")
            .setStyle(
                Notification.MessagingStyle(person)
                    .addMessage("hey there", 1000L, person)
            )
            .build()

        val result = NotificationExtractor.extract(
            key = "key-4", packageName = "com.example", appName = "Example",
            postTimeMillis = 1000L, notification = notification,
        )

        assertTrue(result.expandedLines.any { it.contains("hey there") })
    }

    @Test
    fun `handles missing fields without crashing`() {
        val notification = Notification.Builder(context(), "channel").build()

        val result = NotificationExtractor.extract(
            key = "key-5", packageName = "com.example", appName = "Example",
            postTimeMillis = 1000L, notification = notification,
        )

        assertEquals(null, result.title)
        assertEquals(null, result.body)
        assertTrue(result.expandedLines.isEmpty())
    }

    @Test
    fun `flags ongoing notifications`() {
        val notification = Notification.Builder(context(), "channel")
            .setOngoing(true)
            .build()

        val result = NotificationExtractor.extract(
            key = "key-6", packageName = "com.example", appName = "Example",
            postTimeMillis = 1000L, notification = notification,
        )

        assertTrue(result.isOngoing)
    }
}
