package com.samho.pmcwhandroid.scan

import androidx.compose.runtime.Composable
import androidx.compose.ui.platform.LocalContext
import com.google.mlkit.vision.barcode.common.Barcode
import com.google.mlkit.vision.codescanner.GmsBarcodeScanning
import com.google.mlkit.vision.codescanner.GmsBarcodeScannerOptions

/**
 * Quét mã bằng camera thiết bị (PDA lẫn điện thoại thường) — dùng Google Code Scanner module:
 * Google tự lo màn hình camera + quyền CAMERA, app không cần tự dựng CameraX hay xin quyền.
 * Trả về [onResult] với nội dung mã đọc được; [onError]/hủy quét thì bỏ qua im lặng.
 */
@Composable
fun rememberBarcodeScanner(onResult: (String) -> Unit, onError: (String) -> Unit = {}): () -> Unit {
    val context = LocalContext.current
    return {
        val options = GmsBarcodeScannerOptions.Builder()
            .setBarcodeFormats(Barcode.FORMAT_ALL_FORMATS)
            .build()
        GmsBarcodeScanning.getClient(context, options).startScan()
            .addOnSuccessListener { barcode -> barcode.rawValue?.let(onResult) }
            .addOnFailureListener { e -> onError(e.message ?: "Không quét được, thử lại.") }
    }
}
