package com.samho.pmcwhandroid.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Error
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.material3.TopAppBar
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.InboundRequest
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.StorageLocationDto
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.rememberBarcodeScanner
import kotlinx.coroutines.launch

private data class ScanLogEntry(val id: Long, val barcode: String, val success: Boolean, val message: String)

/**
 * Chọn 1 kệ rồi quét liên tục — mỗi mã quét được tự động lên thẳng kệ đang chọn, không phải chọn
 * lại kệ mỗi lần (thực tế 1 kệ thường nhận nhiều mã cùng lúc, quét dồn cho nhanh).
 * Mỗi lần quét là 1 thao tác ghi độc lập — KHÔNG hủy job cũ khi quét mã mới (khác màn Tìm kiếm),
 * vì hủy job ở đây đồng nghĩa bỏ qua luôn việc nhập kho cho mã vừa quét mà người dùng không biết.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NhapKhoScreen(session: UserSession, onBack: () -> Unit) {
    var stagingItems by remember { mutableStateOf<List<MaterialListItem>>(emptyList()) }
    var isLoadingList by remember { mutableStateOf(true) }
    var currentLocation by remember { mutableStateOf<StorageLocationDto?>(null) }
    var locations by remember { mutableStateOf<List<StorageLocationDto>>(emptyList()) }
    var isLoadingLocations by remember { mutableStateOf(false) }
    var showLocationPicker by remember { mutableStateOf(false) }
    var log by remember { mutableStateOf<List<ScanLogEntry>>(emptyList()) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    suspend fun loadStaging() {
        isLoadingList = true
        try {
            stagingItems = ApiClient.materialsApi.list(status = "Staging", pageSize = 50).items
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Không tải được danh sách: ${e.message}")
        } finally {
            isLoadingList = false
        }
    }

    LaunchedEffect(Unit) { loadStaging() }

    fun openLocationPicker() {
        showLocationPicker = true
        if (locations.isEmpty()) {
            isLoadingLocations = true
            scope.launch {
                try {
                    locations = ApiClient.materialsApi.storageLocations()
                } catch (e: Exception) {
                    snackbarHostState.showSnackbar("Không tải được danh sách kệ: ${e.message}")
                } finally {
                    isLoadingLocations = false
                }
            }
        }
    }

    fun addLog(barcode: String, success: Boolean, message: String) {
        log = (listOf(ScanLogEntry(System.nanoTime(), barcode, success, message)) + log).take(20)
    }

    suspend fun processBarcode(code: String) {
        val loc = currentLocation
        if (loc == null) {
            snackbarHostState.showSnackbar("Hãy chọn kệ trước khi quét.")
            openLocationPicker()
            return
        }
        try {
            val resp = ApiClient.materialsApi.getByBarcode(code)
            val item = resp.body()
            if (!resp.isSuccessful || item == null) {
                addLog(code, false, resp.errorMessageOrDefault("Không tìm thấy mã '$code'."))
                return
            }
            if (item.status != "Staging") {
                addLog(item.barcode, false, "Đang '${item.status}', không thể nhập kho.")
                return
            }
            val inResp = ApiClient.materialsApi.inbound(
                item.materialId,
                InboundRequest(locationId = loc.locationId, userId = session.userId),
            )
            if (inResp.isSuccessful) {
                addLog(item.barcode, true, "Đã lên kệ ${loc.code}")
                loadStaging()
            } else {
                addLog(item.barcode, false, inResp.errorMessageOrDefault("Nhập kho thất bại."))
            }
        } catch (e: Exception) {
            addLog(code, false, "Lỗi mạng: ${e.message}")
        }
    }

    val scanLauncher = rememberBarcodeScanner(
        onResult = { code -> scope.launch { processBarcode(code) } },
        onError = { msg -> scope.launch { snackbarHostState.showSnackbar(msg) } },
    )

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Nhập kho") },
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
        Column(modifier = Modifier.fillMaxSize().padding(padding)) {
            // Thanh chọn kệ — luôn hiện trên cùng, khóa 1 kệ cho cả đợt quét.
            Card(
                modifier = Modifier.fillMaxWidth().padding(12.dp),
                onClick = { openLocationPicker() },
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer),
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth().padding(16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text(
                        currentLocation?.let { "Đang lên kệ: ${it.code}" } ?: "Chưa chọn kệ — bấm để chọn",
                        style = MaterialTheme.typography.titleMedium,
                    )
                    TextButton(onClick = { openLocationPicker() }) {
                        Text(if (currentLocation == null) "Chọn kệ" else "Đổi kệ")
                    }
                }
            }

            if (log.isNotEmpty()) {
                LazyColumn(modifier = Modifier.fillMaxWidth().height(160.dp)) {
                    items(log, key = { it.id }) { entry ->
                        ListItem(
                            leadingContent = {
                                Icon(
                                    if (entry.success) Icons.Filled.CheckCircle else Icons.Filled.Error,
                                    contentDescription = null,
                                    tint = if (entry.success) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.error,
                                )
                            },
                            headlineContent = { Text(entry.barcode) },
                            supportingContent = { Text(entry.message) },
                        )
                    }
                }
                HorizontalDivider()
            }

            Box(modifier = Modifier.fillMaxSize()) {
                when {
                    isLoadingList -> CircularProgressIndicator(modifier = Modifier.align(Alignment.TopCenter).padding(top = 24.dp))
                    stagingItems.isEmpty() -> Text(
                        "Không có liệu đang chờ nhập.",
                        modifier = Modifier.align(Alignment.TopCenter).padding(24.dp),
                    )
                    else -> LazyColumn(modifier = Modifier.fillMaxSize()) {
                        items(stagingItems, key = { it.materialId }) { item ->
                            Card(
                                modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp),
                                onClick = { scope.launch { processBarcode(item.barcode) } },
                            ) {
                                ListItem(
                                    headlineContent = { Text(item.barcode) },
                                    supportingContent = {
                                        Text(listOfNotNull(item.dev, item.model).joinToString(" / "))
                                    },
                                    trailingContent = {
                                        Text("${item.arrivalQty ?: 0} ${item.unit ?: ""}")
                                    },
                                )
                            }
                        }
                    }
                }
            }
        }
    }

    if (showLocationPicker) {
        LocationPickerDialog(
            locations = locations,
            isLoading = isLoadingLocations,
            onDismiss = { showLocationPicker = false },
            onSelect = { loc -> currentLocation = loc; showLocationPicker = false },
        )
    }
}

@Composable
private fun LocationPickerDialog(
    locations: List<StorageLocationDto>,
    isLoading: Boolean,
    onDismiss: () -> Unit,
    onSelect: (StorageLocationDto) -> Unit,
) {
    var query by remember { mutableStateOf("") }
    val filtered = remember(locations, query) {
        if (query.isBlank()) locations else locations.filter { it.code.contains(query, ignoreCase = true) }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = onDismiss) { Text("Đóng") } },
        title = { Text("Chọn kệ") },
        text = {
            Column {
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    label = { Text("Tìm theo mã kệ (vd: 40.1)") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(Modifier.height(8.dp))
                Box(modifier = Modifier.fillMaxWidth().height(320.dp)) {
                    if (isLoading) {
                        CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                    } else {
                        LazyColumn {
                            items(filtered, key = { it.locationId }) { loc ->
                                ListItem(
                                    headlineContent = { Text(loc.code) },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .clickable { onSelect(loc) },
                                )
                                HorizontalDivider()
                            }
                        }
                    }
                }
            }
        },
    )
}
