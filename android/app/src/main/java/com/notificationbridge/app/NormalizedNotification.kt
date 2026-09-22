package com.notificationbridge.app

data class NormalizedNotification(
    val key: String,
    val packageName: String,
    val appName: String,
    val title: String?,
    val body: String?,
    val bigText: String?,
    val expandedLines: List<String>,
    val summary: String?,
    val subText: String?,
    val timestamp: Long,
    val category: String?,
    val hasIcon: Boolean,
    val isOngoing: Boolean,
)
