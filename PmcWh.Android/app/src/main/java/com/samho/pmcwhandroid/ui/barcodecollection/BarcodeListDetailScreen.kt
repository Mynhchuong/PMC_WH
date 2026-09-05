package com.samho.pmcwhandroid.ui.barcodecollection

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material.icons.filled.Close
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
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
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.BarcodeListDto
import com.samho.pmcwhandroid.network.BarcodeListItemDto
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.ScanBarcodeRequest
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.ContinuousBarcodeScannerDialog
import com.samho.pmcwhandroid.scan.DataWedgeScanField
import com.samho.pmcwhandroid.scan.ScanFeedback
import com.samho.pmcwhandroid.scan.ScanFeedbackBanner
import com.samho.pmcwhandroid.ui.components.MaterialDetailDialog
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.Job
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch

/**
 * Quét cho 1 Barcode List — lưu ngay khi quét (mỗi mã gọi API `scan` ngay lập tức, KHÔNG có nút
 * "Lưu" theo lô như Xuất kho/Nhập kho, đúng yêu cầu PMC "quét tới đâu lưu tới đó"). Mã trùng →
 * ScanCount +1 phía server, hiện cảnh báo (tái dùng ScanFeedback.Failure để có tiếng cảnh báo/rung
 * khác biệt sẵn có). Xem/xuất Excel thật/xoá cả list thì làm bên Web cho dễ (PMC feedback) — màn
 * này chỉ quét + xoá được từng mã lỡ quét nhầm.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun BarcodeListDetailScreen(list: BarcodeListDto, onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    var items by remember { mutableStateOf<List<BarcodeListItemDto>>(emptyList()) }
    var isLoading by remember { mutableStateOf(false) }
    var showCamera by remember { mutableStateOf(false) }
    var lastFeedback by remember { mutableStateOf<ScanFeedback?>(null) }
    var viewingDetail by remember { mutableStateOf<MaterialListItem?>(null) }
    var loadingDetailForItemId by remember { mutableStateOf<Int?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val scanChannel = remember { Channel<String>(Channel.UNLIMITED) }
    var reloadJob by remember { mutableStateOf<Job?>(null) }

    // Barcode Collection chỉ lưu chuỗi Barcode, không có MaterialId — bấm vào 1 dòng thì tra cứu
    // qua by-barcode() để hiện popup chi tiết liệu, y hệt kiểu xem đang có ở Xuất kho/Nhận lại/Hủy
    // liệu. Không phải barcode nào ở đây cũng khớp 1 liệu thật (VD mã tự đặt để đếm rời) — báo rõ
    // nếu không tìm thấy thay vì im lặng không phản hồi khi bấm.
    fun openDetail(item: BarcodeListItemDto) {
        loadingDetailForItemId = item.itemId
        scope.launch {
            try {
                val resp = ApiClient.materialsApi.getByBarcode(item.barcode)
                val material = resp.body()
                if (resp.isSuccessful && material != null) {
                    viewingDetail = material
                } else {
                    snackbarHostState.showSnackbar("Mã \"${item.barcode}\" không có trong danh sách liệu.")
                }
            } catch (e: Exception) {
                snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
            } finally {
                loadingDetailForItemId = null
            }
        }
    }

    suspend fun loadItems() {
        isLoading = true
        try {
            items = ApiClient.barcodeCollectionApi.getItems(list.listId)
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Không tải được danh sách: ${e.message}")
        } finally {
            isLoading = false
        }
    }

    // Nhiều nguồn cùng yêu cầu tải lại (quét xong 1 mã / xoá 1 mã) — huỷ lần tải trước để "lần cuối
    // thắng", tránh response cũ về sau đè lên danh sách mới.
    fun reloadItems() {
        reloadJob?.cancel()
        reloadJob = scope.launch { loadItems() }
    }

    LaunchedEffect(Unit) { loadItems() }

    LaunchedEffect(Unit) {
        for (code in scanChannel) {
            try {
                val resp = ApiClient.barcodeCollectionApi.scan(list.listId, ScanBarcodeRequest(barcode = code))
                val result = resp.body()
                lastFeedback = when {
                    !resp.isSuccessful || result == null ->
                        ScanFeedback.Failure(code, resp.errorMessageOrDefault("Quét lỗi cho mã '$code'."))
                    result.isDuplicate ->
                        ScanFeedback.Failure(code, "Đã quét trùng — số lần quét: ${result.scanCount}")
                    else -> ScanFeedback.Success(code, "Đã thêm mã mới.")
                }
                reloadItems()
            } catch (e: Exception) {
                lastFeedback = ScanFeedback.Failure(code, "Lỗi mạng: ${e.message}")
            }
        }
    }

    val totalScans = items.sumOf { it.scanCount }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text(list.name)
                        Text(
                            "${items.size} mã · $totalScans lượt quét",
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                },
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
        Column(modifier = Modifier.fillMaxSize().padding(padding)) {
            DataWedgeScanField(
                onScan = { code -> scanChannel.trySend(code) },
                // Tắt khi đang mở popup chi tiết liệu.
                enabled = viewingDetail == null,
                modifier = Modifier.fillMaxWidth().padding(12.dp),
            )
            ScanFeedbackBanner(feedback = lastFeedback, onDismiss = { lastFeedback = null })

            Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                if (isLoading && items.isEmpty()) {
                    CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                } else if (items.isEmpty()) {
                    Text(
                        "Quét mã để thêm vào list này.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    )
                } else {
                    LazyColumn(modifier = Modifier.fillMaxSize()) {
                        items(items, key = { it.itemId }) { item ->
                            Card(
                                modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp)
                                    .clickable { openDetail(item) },
                            ) {
                                Row(
                                    modifier = Modifier.fillMaxWidth().padding(12.dp),
                                    horizontalArrangement = Arrangement.SpaceBetween,
                                    verticalAlignment = Alignment.CenterVertically,
                                ) {
                                    Text(item.barcode, style = MaterialTheme.typography.titleMedium)
                                    Row(verticalAlignment = Alignment.CenterVertically) {
                                        if (loadingDetailForItemId == item.itemId) {
                                            CircularProgressIndicator(modifier = Modifier.size(18.dp), strokeWidth = 2.dp)
                                            Spacer(Modifier.width(10.dp))
                                        }
                                        Text("x${item.scanCount}", style = MaterialTheme.typography.titleMedium)
                                        IconButton(onClick = {
                                            scope.launch {
                                                try {
                                                    ApiClient.barcodeCollectionApi.deleteItem(list.listId, item.itemId)
                                                    reloadItems()
                                                } catch (e: Exception) {
                                                    snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
                                                }
                                            }
                                        }) {
                                            Icon(Icons.Filled.Close, contentDescription = "Xoá mã")
                                        }
                                    }
                                }
                            }
                            HorizontalDivider()
                        }
                    }
                }
            }
        }
    }

    if (showCamera) {
        ContinuousBarcodeScannerDialog(
            onBarcodeScanned = { code -> scanChannel.trySend(code) },
            onClose = { showCamera = false },
            lastFeedback = lastFeedback,
            onDismissFeedback = { lastFeedback = null },
        )
    }

    viewingDetail?.let { material ->
        MaterialDetailDialog(item = material, onDismiss = { viewingDetail = null })
    }
}

@Preview(showBackground = true)
@Composable
private fun BarcodeListDetailScreenPreview() {
    PmcWhAndroidTheme {
        BarcodeListDetailScreen(
            list = BarcodeListDto(listId = 1, name = "Liệu test 20/08", itemCount = 2, totalScans = 3),
            onBack = {},
        )
    }
}
