package com.notificationbridge.app

import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import org.junit.runner.RunWith
import org.robolectric.RobolectricTestRunner

// org.json is only functional under Robolectric on the JVM.
@RunWith(RobolectricTestRunner::class)
class PairingClientTest {

    private fun parse(text: String) = PairingClient.parsePairResponse(text, "192.168.1.7", 7787, "FP", "device-1")

    @Test
    fun successfulResponse_becomesATrustedPcWithTheSecret() {
        val result = parse("""{"messageType":"PAIR_RESPONSE","payload":{"success":true,"sharedSecret":"c2VjcmV0"}}""")

        val pc = (result as PairingClient.Result.Success).pc
        assertEquals("c2VjcmV0", pc.sharedSecretBase64)
        assertEquals("FP", pc.certFingerprint)
        assertEquals("device-1", pc.deviceId)
    }

    @Test
    fun rejectedResponse_carriesTheReason() {
        val result = parse("""{"messageType":"PAIR_RESPONSE","payload":{"success":false,"error":"invalid or expired pairing code"}}""")

        assertEquals("invalid or expired pairing code", (result as PairingClient.Result.Failure).reason)
    }

    @Test
    fun otherMessageTypes_areIgnored() {
        assertNull(parse("""{"messageType":"AUTH_RESULT","payload":{}}"""))
    }

    @Test
    fun malformedResponses_failInsteadOfThrowing() {
        assertTrue(parse("not json") is PairingClient.Result.Failure)
        // Claims success but omits the secret.
        assertTrue(parse("""{"messageType":"PAIR_RESPONSE","payload":{"success":true}}""") is PairingClient.Result.Failure)
    }
}
