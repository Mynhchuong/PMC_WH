package com.samho.pmcwhandroid.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.ContinuousBarcodeScannerDialog
import com.samho.pmcwhandroid.scan.DataWedgeScanField
import com.samho.pmcwhandroid.scan.ScanTone
import com.samho.pmcwhandroid.ui.components.MaterialDetailBody
import com.samho.pmcwhandroid.ui.components.StatusBadge
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TimKiemScreen(onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    val context = LocalContext.current
    var result by remember { mutableStateOf<MaterialListItem?>(null) }
    var notFoundMsg by remember { mutableStateOf<String?>(null) }
    var isSearching by remember { mutableStateOf(false) }
    var showCamera by remember { mutableStateOf(false) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    // Quét/tìm liên tục (đặc thù PDA) có thể bắn nhiều request cùng lúc — hủy request cũ trước khi
    // bắt đầu request mới, tránh response cũ về sau đè lên kết quả đang xem.
    var searchJob by remember { mutableStateOf<Job?>(null) }

    suspend fun search(code: String) {
        val trimmed = code.trim()
        if (trimmed.isBlank()) return
        isSearching = true
        result = null
        notFoundMsg = null
        try {
            val resp = ApiClient.materialsApi.getByBarcode(trimmed)
            val item = resp.body()
            if (resp.isSuccessful && item != null) {
                result = item
                ScanTone.success()
            } else {
                notFoundMsg = resp.errorMessageOrDefault("Không tìm thấy mã '$trimmed'.")
                ScanTone.failure(context)
            }
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
            ScanTone.failure(context)
        } finally {
            isSearching = false
        }
    }

    fun launchSearch(code: String) {
        searchJob?.cancel()
        searchJob = scope.launch { search(code) }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Tìm kiếm") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Quay lại")
                    }
                },
                actions = {
                    IconButton(onClick = { showCamera = true }) {
                        Icon(Icons.Filled.CameraAlt, contentDescription = "Quét bằng camera")
                    }
                },
            )
        },
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp)) {
            DataWedgeScanField(
                onScan = { code -> launchSearch(code) },
                label = "Nhập hoặc quét barcode",
                enabled = !showCamera,
                modifier = Modifier.fillMaxWidth(),
            )

            Spacer(Modifier.height(16.dp))

            Box(modifier = Modifier.fillMaxSize()) {
                when {
                    isSearching -> CircularProgressIndicator(modifier = Modifier.align(Alignment.TopCenter))
                    notFoundMsg != null -> Text(
                        notFoundMsg ?: "",
                        color = MaterialTheme.colorScheme.error,
                        modifier = Modifier.align(Alignment.TopCenter).padding(top = 24.dp),
                    )
                    result != null -> MaterialResultCard(result!!)
                }
            }
        }
    }

    if (showCamera) {
        // Tìm kiếm chỉ cần 1 kết quả tại 1 thời điểm — quét xong 1 mã là đóng camera luôn để thấy
        // ngay kết quả bên dưới, khác với các màn quét theo lô (không tự đóng).
        ContinuousBarcodeScannerDialog(
            onBarcodeScanned = { code -> launchSearch(code); showCamera = false },
            onClose = { showCamera = false },
        )
    }
}

@Composable
private fun MaterialResultCard(item: MaterialListItem) {
    Card(modifier = Modifier.fillMaxWidth()) {
        Column(modifier = Modifier.padding(16.dp)) {
            Row(horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                Text(item.barcode, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                StatusBadge(item.status, item.isOverdue)
            }

            Spacer(Modifier.height(12.dp))

            MaterialDetailBody(item)
        }
    }
}

private val sampleSearchResult = MaterialListItem(
    materialId = 1,
    barcode = "QATEST001",
    dev = "QA/BUGTEST",
    poNo = "PO-2026-001",
    supplier = "Nhà cung cấp A",
    model = "Model X",
    colorway = "Đen",
    sizeSpec = "M",
    matlDescription = "Vải lót áo khoác",
    colorCode = "BLK",
    arrivalQty = 100.0,
    balance = 42.0,
    unit = "M",
    status = "InStock",
    locationCode = "12.3",
    isOverdue = false,
)

@Preview(showBackground = true, name = "Chưa tìm")
@Composable
private fun TimKiemScreenEmptyPreview() {
    PmcWhAndroidTheme {
        TimKiemScreen(onBack = {})
    }
}

@Preview(showBackground = true, name = "Có kết quả")
@Composable
private fun TimKiemScreenResultPreview() {
    PmcWhAndroidTheme {
        MaterialResultCard(sampleSearchResult)
    }
}
