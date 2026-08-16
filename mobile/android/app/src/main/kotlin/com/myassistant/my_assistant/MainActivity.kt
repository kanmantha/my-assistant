package com.myassistant.my_assistant

import android.content.ActivityNotFoundException
import android.content.Intent
import android.net.Uri
import io.flutter.embedding.android.FlutterActivity
import io.flutter.embedding.engine.FlutterEngine
import io.flutter.plugin.common.MethodChannel

class MainActivity : FlutterActivity() {
    override fun configureFlutterEngine(flutterEngine: FlutterEngine) {
        super.configureFlutterEngine(flutterEngine)
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
    }

    private fun openInChrome(url: String): Boolean {
        // 1) Explicit Chrome intent so no browser chooser is shown.
        val chrome = Intent(Intent.ACTION_VIEW, Uri.parse(url)).apply {
            setPackage("com.android.chrome")
            addFlags(Intent.FLAG_ACTIVITY_NEW_TASK)
        }
        if (isResolvable(chrome)) {
            startActivity(chrome)
            return true
        }

        // 2) Chrome may not be installed: fall back to any default browser.
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