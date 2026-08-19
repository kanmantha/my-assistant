package com.myassistant.my_assistant

import android.Manifest
import android.content.ActivityNotFoundException
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.os.Bundle
import android.speech.RecognitionListener
import android.speech.RecognizerIntent
import android.speech.SpeechRecognizer
import androidx.core.app.ActivityCompat
import androidx.core.content.ContextCompat
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.EventChannel
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    private var speechRecognizer: SpeechRecognizer? = null
    private var eventSink: EventChannel.EventSink? = null

    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)

        // Chrome channel
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "myassistant/chrome")
            .setMethodCallHandler { call, result ->
                if (call.method == "openUrl") {
                    val url = call.argument<String>("url")
                    if (url.isNullOrBlank() || !isHttp(url)) {
                        result.success(false)
                        return@setMethodCallHandler
                    }
                    result.success(openInChrome(url))
                } else {
                    result.notImplemented()
                }
            }

        // Permission channel
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "myassistant/permissions")
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "checkMicPermission" -> {
                        val granted = ContextCompat.checkSelfPermission(
                            this, Manifest.permission.RECORD_AUDIO
                        ) == PackageManager.PERMISSION_GRANTED
                        result.success(granted)
                    }
                    "requestMicPermission" -> {
                        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO)
                            == PackageManager.PERMISSION_GRANTED) {
                            result.success(true)
                        } else {
                            ActivityCompat.requestPermissions(
                                this,
                                arrayOf(Manifest.permission.RECORD_AUDIO),
                                1001
                            )
                            result.success(false)
                        }
                    }
                    "hasSpeechService" -> {
                        val has = SpeechRecognizer.isRecognitionAvailable(this)
                        result.success(has)
                    }
                    "openAppSettings" -> {
                        try {
                            val intent = Intent(android.provider.Settings.ACTION_APPLICATION_DETAILS_SETTINGS)
                            intent.data = Uri.fromParts("package", packageName, null)
                            startActivity(intent)
                            result.success(true)
                        } catch (e: Exception) {
                            result.success(false)
                        }
                    }
                    else -> result.notImplemented()
                }
            }

        // Native speech channel — bypasses speech_to_text plugin entirely
        MethodChannel(flutterEngine.dartExecutor.binaryMessenger, "myassistant/native_speech")
            .setMethodCallHandler { call, result ->
                when (call.method) {
                    "startListening" -> {
                        val language = call.argument<String>("language") ?: "en-IN"
                        startNativeSpeech(language)
                        result.success(true)
                    }
                    "stopListening" -> {
                        stopNativeSpeech()
                        result.success(true)
                    }
                    "isAvailable" -> {
                        result.success(SpeechRecognizer.isRecognitionAvailable(this))
                    }
                    else -> result.notImplemented()
                }
            }

        EventChannel(flutterEngine.dartExecutor.binaryMessenger, "myassistant/native_speech_events")
            .setStreamHandler(object : EventChannel.StreamHandler {
                override fun onListen(arguments: Any?, sink: EventChannel.EventSink?) {
                    eventSink = sink
                }
                override fun onCancel(arguments: Any?) {
                    eventSink = null
                }
            })
    }

    private fun startNativeSpeech(language: String) {
        stopNativeSpeech()

        if (ContextCompat.checkSelfPermission(this, Manifest.permission.RECORD_AUDIO)
            != PackageManager.PERMISSION_GRANTED) {
            eventSink?.error("PERMISSION_DENIED", "Microphone permission not granted", null)
            return
        }

        if (!SpeechRecognizer.isRecognitionAvailable(this)) {
            eventSink?.error("NO_SPEECH_SERVICE", "No speech recognition service available", null)
            return
        }

        speechRecognizer = SpeechRecognizer.createSpeechRecognizer(this)
        speechRecognizer?.setRecognitionListener(object : RecognitionListener {
            override fun onReadyForSpeech(params: Bundle?) {
                eventSink?.success(mapOf("type" to "ready"))
            }
            override fun onBeginningOfSpeech() {
                eventSink?.success(mapOf("type" to "speech_started"))
            }
            override fun onRmsChanged(rmsdB: Float) {}
            override fun onBufferReceived(buffer: ByteArray?) {}
            override fun onEndOfSpeech() {
                eventSink?.success(mapOf("type" to "speech_ended"))
            }
            override fun onError(error: Int) {
                val msg = when (error) {
                    SpeechRecognizer.ERROR_NO_MATCH -> "NO_MATCH"
                    SpeechRecognizer.ERROR_SPEECH_TIMEOUT -> "TIMEOUT"
                    SpeechRecognizer.ERROR_AUDIO -> "AUDIO_ERROR"
                    SpeechRecognizer.ERROR_CLIENT -> "CLIENT_ERROR"
                    SpeechRecognizer.ERROR_SERVER -> "SERVER_ERROR"
                    SpeechRecognizer.ERROR_NETWORK -> "NETWORK_ERROR"
                    SpeechRecognizer.ERROR_NETWORK_TIMEOUT -> "NETWORK_TIMEOUT"
                    SpeechRecognizer.ERROR_RECOGNIZER_BUSY -> "BUSY"
                    SpeechRecognizer.ERROR_INSUFFICIENT_PERMISSIONS -> "PERMISSION_DENIED"
                    else -> "UNKNOWN_$error"
                }
                eventSink?.success(mapOf("type" to "error", "error" to msg))
            }
            override fun onResults(results: Bundle?) {
                val matches = results?.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION)
                val text = matches?.firstOrNull() ?: ""
                eventSink?.success(mapOf("type" to "result", "text" to text, "final" to true))
            }
            override fun onPartialResults(partialResults: Bundle?) {
                val matches = partialResults?.getStringArrayList(SpeechRecognizer.RESULTS_RECOGNITION)
                val text = matches?.firstOrNull() ?: ""
                if (text.isNotEmpty()) {
                    eventSink?.success(mapOf("type" to "result", "text" to text, "final" to false))
                }
            }
            override fun onEvent(eventType: Int, params: Bundle?) {}
        })

        val intent = Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH).apply {
            putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM)
            putExtra(RecognizerIntent.EXTRA_LANGUAGE, language)
            putExtra(RecognizerIntent.EXTRA_PARTIAL_RESULTS, true)
            putExtra(RecognizerIntent.EXTRA_MAX_RESULTS, 1)
        }

        speechRecognizer?.startListening(intent)
    }

    private fun stopNativeSpeech() {
        try {
            speechRecognizer?.stopListening()
        } catch (_: Exception) {}
        try {
            speechRecognizer?.cancel()
        } catch (_: Exception) {}
        try {
            speechRecognizer?.destroy()
        } catch (_: Exception) {}
        speechRecognizer = null
    }

    override fun onDestroy() {
        stopNativeSpeech()
        super.onDestroy()
    }

    private fun openInChrome(url: String): Boolean {
        val chrome = Intent(Intent.ACTION_VIEW, Uri.parse(url)).apply {
            setPackage("com.android.chrome")
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        if (isResolvable(chrome)) {
            startActivity(chrome)
            return true
        }
        val fallback = Intent(Intent.ACTION_VIEW, Uri.parse(url)).apply {
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        if (isResolvable(fallback)) {
            startActivity(fallback)
            return true
        }
        return false
    }

    private fun isResolvable(intent: Intent): Boolean =
        runCatching {
            intent.resolveActivity(packageManager)?.packageName != null
        }.getOrDefault(false)

    private fun isHttp(url: String): Boolean {
        val lower = url.lowercase()
        return lower.startsWith("http://") || lower.startsWith("https://")
    }
}
