package com.notificationbridge.app

import java.util.concurrent.CopyOnWriteArrayList

// In-memory only: no persistence, per ADR-005. Bridges the listener service to the dev inspector
// UI within the same process.
object NotificationRepository {

    private val entries = CopyOnWriteArrayList<NormalizedNotification>()
    private val listeners = CopyOnWriteArrayList<() -> Unit>()

    fun upsert(notification: NormalizedNotification) {
        entries.removeAll { it.key == notification.key }
        entries.add(0, notification)
        notifyListeners()
    }

    fun remove(key: String) {
        entries.removeAll { it.key == key }
        notifyListeners()
    }

    fun snapshot(): List<NormalizedNotification> = entries.toList()

    fun addListener(listener: () -> Unit) {
        listeners.add(listener)
    }

    fun removeListener(listener: () -> Unit) {
        listeners.remove(listener)
    }

    private fun notifyListeners() {
        listeners.forEach { it() }
    }
}
