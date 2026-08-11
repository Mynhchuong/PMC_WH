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
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilledIconButton
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.rememberBarcodeScanner
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun TimKiemScreen(onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    var query by remember { mutableStateOf("") }
    var result by remember { mutableStateOf<MaterialListItem?>(null) }
    var notFoundMsg by remember { mutableStateOf<String?>(null) }
    var isSearching by remember { mutableStateOf(false) }
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
            } else {
                notFoundMsg = resp.errorMessageOrDefault("Không tìm thấy mã '$trimmed'.")
            }
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
        } finally {
            isSearching = false
        }
    }

    fun launchSearch(code: String) {
        searchJob?.cancel()
        searchJob = scope.launch { search(code) }
    }

    val scanLauncher = rememberBarcodeScanner(
        onResult = { code ->
            query = code
            launchSearch(code)
        },
        onError = { msg -> scope.launch { snackbarHostState.showSnackbar(msg) } },
    )

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
                    IconButton(onClick = scanLauncher) {
                        Icon(Icons.Filled.QrCodeScanner, contentDescription = "Quét mã")
                    }
                },
            )
        },
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding).padding(16.dp)) {
            Row(verticalAlignment = Alignment.CenterVertically) {
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    label = { Text("Nhập hoặc quét barcode") },
                    singleLine = true,
                    keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
                    keyboardActions = KeyboardActions(onSearch = { launchSearch(query) }),
                    modifier = Modifier.weight(1f).padding(end = 8.dp),
                )
                FilledIconButton(onClick = { launchSearch(query) }) {
                    Icon(Icons.Filled.Search, contentDescription = "Tìm")
                }
            }

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

            InfoRow("Vị trí", item.locationCode ?: "— (không trong kho)")
            InfoRow("Dev", item.dev ?: "—")
            InfoRow("PO", item.poNo ?: "—")
            InfoRow("Nhà cung cấp", item.supplier ?: "—")
            InfoRow("Model", item.model ?: "—")
            InfoRow("Màu/Size", listOfNotNull(item.colorway, item.sizeSpec).joinToString(" / ").ifBlank { "—" })
            InfoRow("Mô tả", item.matlDescription ?: "—")
            InfoRow("SL nhập", "${item.arrivalQty ?: 0} ${item.unit ?: ""}")
            InfoRow("Tồn hiện tại", "${item.balance ?: 0} ${item.unit ?: ""}")
        }
    }
}

@Composable
private fun InfoRow(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
        Text(label, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f))
        Text(value, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium, modifier = Modifier.weight(1.4f))
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

@Composable
private fun StatusBadge(status: String, isOverdue: Boolean) {
    val (bg, fg, label) = when {
        isOverdue -> Triple(MaterialTheme.colorScheme.errorContainer, MaterialTheme.colorScheme.onErrorContainer, "Quá hạn")
        status == "InStock" -> Triple(MaterialTheme.colorScheme.primaryContainer, MaterialTheme.colorScheme.onPrimaryContainer, "Trong kho")
        status == "PartiallyIssued" -> Triple(MaterialTheme.colorScheme.tertiaryContainer, MaterialTheme.colorScheme.onTertiaryContainer, "Xuất 1 phần")
        status == "IssuedOut" -> Triple(MaterialTheme.colorScheme.surfaceVariant, MaterialTheme.colorScheme.onSurfaceVariant, "Đã xuất hết")
        status == "Staging" -> Triple(MaterialTheme.colorScheme.secondaryContainer, MaterialTheme.colorScheme.onSecondaryContainer, "Chờ nhập")
        status == "Disposed" -> Triple(MaterialTheme.colorScheme.errorContainer, MaterialTheme.colorScheme.onErrorContainer, "Đã hủy")
        else -> Triple(MaterialTheme.colorScheme.surfaceVariant, MaterialTheme.colorScheme.onSurfaceVariant, status)
    }
    Surface(color = bg, contentColor = fg, shape = MaterialTheme.shapes.small) {
        Text(label, style = MaterialTheme.typography.labelMedium, modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp))
    }
}
