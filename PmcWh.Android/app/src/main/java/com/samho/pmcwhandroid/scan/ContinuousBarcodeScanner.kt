@file:OptIn(androidx.camera.core.ExperimentalGetImage::class)

package com.samho.pmcwhandroid.scan

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.net.Uri
import android.provider.Settings
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.camera.core.CameraSelector
import androidx.camera.core.ImageAnalysis
import androidx.camera.core.ImageProxy
import androidx.camera.core.Preview
import androidx.camera.lifecycle.ProcessCameraProvider
import androidx.camera.view.PreviewView
import androidx.compose.foundation.background
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Button
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberUpdatedState
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.text.style.TextAlign
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.compose.ui.window.Dialog
import androidx.compose.ui.window.DialogProperties
import androidx.core.content.ContextCompat
import com.google.mlkit.vision.barcode.BarcodeScanner
import com.google.mlkit.vision.barcode.BarcodeScanning
import com.google.mlkit.vision.common.InputImage
import java.util.concurrent.Executors

/**
 * Camera quét liên tục — mở 1 lần, bắn [onBarcodeScanned] nhiều lần, KHÔNG tự đóng; người dùng bấm
 * nút đóng (góc trên phải) mới thoát. Dùng làm phương án dự phòng cho điện thoại thường không có
 * súng quét cứng — đường quét chính trên PDA vẫn là [DataWedgeScanField] qua nút cứng.
 *
 * [lastFeedback]/[onDismissFeedback]: Dialog này nằm ở cửa sổ riêng (che hết Scaffold của màn
 * chính) nên Snackbar báo kết quả quét của màn chính sẽ KHÔNG hiện được ở đây — phải tự vẽ banner
 * kết quả ngay trong dialog để người quét biết quét được hay lỗi trước khi quét mã tiếp theo.
 */
@Composable
fun ContinuousBarcodeScannerDialog(
    onBarcodeScanned: (String) -> Unit,
    onClose: () -> Unit,
    lastFeedback: ScanFeedback? = null,
    onDismissFeedback: () -> Unit = {},
    debounceMs: Long = 1500L,
) {
    val context = LocalContext.current
    var hasPermission by remember {
        mutableStateOf(
            ContextCompat.checkSelfPermission(context, Manifest.permission.CAMERA) == PackageManager.PERMISSION_GRANTED,
        )
    }
    var permanentlyDenied by remember { mutableStateOf(false) }

    val permissionLauncher = rememberLauncherForActivityResult(ActivityResultContracts.RequestPermission()) { granted ->
        hasPermission = granted
        if (!granted) permanentlyDenied = true
    }

    Dialog(onDismissRequest = onClose, properties = DialogProperties(usePlatformDefaultWidth = false)) {
        Box(modifier = Modifier.fillMaxSize()) {
            if (hasPermission) {
                CameraPreviewWithAnalysis(onBarcodeScanned = onBarcodeScanned, debounceMs = debounceMs)
            } else {
                PermissionRationale(
                    permanentlyDenied = permanentlyDenied,
                    onRequestPermission = { permissionLauncher.launch(Manifest.permission.CAMERA) },
                    onOpenSettings = {
                        context.startActivity(
                            Intent(Settings.ACTION_APPLICATION_DETAILS_SETTINGS)
                                .setData(Uri.fromParts("package", context.packageName, null)),
                        )
                    },
                )
            }

            IconButton(
                onClick = onClose,
                modifier = Modifier.align(Alignment.TopEnd).padding(12.dp),
            ) {
                Icon(Icons.Filled.Close, contentDescription = "Đóng", tint = Color.White)
            }

            ScanFeedbackBanner(
                feedback = lastFeedback,
                onDismiss = onDismissFeedback,
                modifier = Modifier.align(Alignment.BottomCenter).padding(16.dp),
            )
        }
    }
}

@Composable
private fun PermissionRationale(
    permanentlyDenied: Boolean,
    onRequestPermission: () -> Unit,
    onOpenSettings: () -> Unit,
) {
    Box(modifier = Modifier.fillMaxSize().background(Color.Black), contentAlignment = Alignment.Center) {
        Column(horizontalAlignment = Alignment.CenterHorizontally, modifier = Modifier.padding(24.dp)) {
            Text(
                "Cần quyền Camera để quét mã bằng camera.",
                color = Color.White,
                textAlign = TextAlign.Center,
            )
            Spacer(Modifier.height(16.dp))
            if (permanentlyDenied) {
                Button(onClick = onOpenSettings) { Text("Mở Cài đặt") }
            } else {
                Button(onClick = onRequestPermission) { Text("Cấp quyền") }
            }
        }
    }
}

@Composable
private fun CameraPreviewWithAnalysis(
    onBarcodeScanned: (String) -> Unit,
    debounceMs: Long,
) {
    val context = LocalContext.current
    val lifecycleOwner = LocalLifecycleOwner.current
    val onBarcodeScannedState = rememberUpdatedState(onBarcodeScanned)

    var boundProvider by remember { mutableStateOf<ProcessCameraProvider?>(null) }
    val executor = remember { Executors.newSingleThreadExecutor() }
    val barcodeScanner = remember { BarcodeScanning.getClient() }
    val analyzer = remember {
        DedupingBarcodeAnalyzer(debounceMs = debounceMs, barcodeScanner = barcodeScanner) { code ->
            onBarcodeScannedState.value(code)
        }
    }

    DisposableEffect(Unit) {
        onDispose {
            boundProvider?.unbindAll()
            executor.shutdown()
            barcodeScanner.close()
        }
    }

    AndroidView(
        modifier = Modifier.fillMaxSize(),
        factory = { ctx ->
            val previewView = PreviewView(ctx)
            val providerFuture = ProcessCameraProvider.getInstance(ctx)
            providerFuture.addListener({
                val provider = providerFuture.get()
                val preview = Preview.Builder().build().also {
                    it.surfaceProvider = previewView.surfaceProvider
                }
                val imageAnalysis = ImageAnalysis.Builder()
                    .setBackpressureStrategy(ImageAnalysis.STRATEGY_KEEP_ONLY_LATEST)
                    .build()
                    .also { it.setAnalyzer(executor, analyzer) }

                provider.unbindAll()
                provider.bindToLifecycle(
                    lifecycleOwner,
                    CameraSelector.DEFAULT_BACK_CAMERA,
                    preview,
                    imageAnalysis,
                )
                boundProvider = provider
            }, ContextCompat.getMainExecutor(ctx))
            previewView
        },
    )
}

private class DedupingBarcodeAnalyzer(
    private val debounceMs: Long,
    private val barcodeScanner: BarcodeScanner,
    private val onBarcode: (String) -> Unit,
) : ImageAnalysis.Analyzer {
    private var lastCode: String? = null
    private var lastAtMs: Long = 0L

    override fun analyze(imageProxy: ImageProxy) {
        val mediaImage = imageProxy.image
        if (mediaImage == null) {
            imageProxy.close()
            return
        }
        val image = InputImage.fromMediaImage(mediaImage, imageProxy.imageInfo.rotationDegrees)
        barcodeScanner.process(image)
            .addOnSuccessListener { barcodes ->
                val value = barcodes.firstOrNull()?.rawValue
                if (value != null) {
                    val now = System.currentTimeMillis()
                    if (value != lastCode || now - lastAtMs > debounceMs) {
                        lastCode = value
                        lastAtMs = now
                        onBarcode(value)
                    }
                }
            }
            .addOnCompleteListener { imageProxy.close() }
    }
}
