package com.samho.pmcwhandroid.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
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
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.DisposeRequest
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.ContinuousBarcodeScannerDialog
import com.samho.pmcwhandroid.scan.DataWedgeScanField
import com.samho.pmcwhandroid.scan.ScanFeedback
import com.samho.pmcwhandroid.scan.ScanFeedbackBanner
import com.samho.pmcwhandroid.ui.components.InfoRow
import com.samho.pmcwhandroid.ui.components.PendingBatchList
import com.samho.pmcwhandroid.ui.components.PendingRow
import com.samho.pmcwhandroid.ui.components.PendingRowStatus
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch

private data class DisposePendingRow(
    override val key: Long,
    val item: MaterialListItem,
    override val status: PendingRowStatus = PendingRowStatus.PENDING,
    override val error: String? = null,
) : PendingRow

/**
 * Quét theo lô: mỗi mã quét được so khớp với danh sách liệu có thể hủy (tải lại trước khi so khớp —
 * danh sách trên máy có thể đã cũ) rồi thêm vào danh sách chờ, KHÔNG hủy ngay. "Lưu" xác nhận 1 lần
 * cho cả lô rồi mới gọi dispose() lần lượt từng liệu. Cố ý KHÔNG hiện danh sách để bấm chọn thủ công
 * — chỉ quét mới thêm được vào danh sách chờ, tránh công nhân bấm nhầm liệu (đặc biệt nguy hiểm ở
 * màn này vì hủy liệu không thể hoàn tác).
 *
 * Hủy ở BẤT KỲ trạng thái nào (Staging/InStock/PartiallyIssued/IssuedOut...) — không giới hạn theo
 * quá hạn 90 ngày, vì công nhân hủy liệu khi không còn dùng nữa, bất kể trạng thái hiện tại là gì.
 * Riêng nghiệp vụ "xuất quá hạn 90 ngày không nhận lại → xóa" là chuyện KHÁC, chỉ có trên web
 * (Overdue/Index).
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HuyLieuScreen(session: UserSession, onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    var disposableItems by remember { mutableStateOf<List<MaterialListItem>>(emptyList()) }
    var pendingDispose by remember { mutableStateOf<List<DisposePendingRow>>(emptyList()) }
    var showConfirm by remember { mutableStateOf(false) }
    var isSaving by remember { mutableStateOf(false) }
    var showCamera by remember { mutableStateOf(false) }
    var lastFeedback by remember { mutableStateOf<ScanFeedback?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    // Gom cả 2 nguồn quét (DataWedge + camera) qua 1 channel, xử lý tuần tự từng mã một — tránh
    // mất dữ liệu nếu 2 nguồn bắn mã gần như cùng lúc rồi cùng đọc/ghi "disposableItems" cũ.
    val scanChannel = remember { Channel<String>(Channel.UNLIMITED) }

    suspend fun loadDisposable() {
        try {
            disposableItems = ApiClient.materialsApi.disposable()
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Không tải được danh sách: ${e.message}")
        }
    }

    LaunchedEffect(Unit) { loadDisposable() }

    fun addToPending(item: MaterialListItem): Boolean {
        if (pendingDispose.any { it.item.materialId == item.materialId }) return false
        pendingDispose = pendingDispose + DisposePendingRow(key = System.nanoTime(), item = item)
        return true
    }

    LaunchedEffect(Unit) {
        for (code in scanChannel) {
            loadDisposable()
            val match = disposableItems.firstOrNull { it.barcode.equals(code, ignoreCase = true) }
            lastFeedback = when {
                match == null -> ScanFeedback.Failure(code, "Không tìm thấy mã này, hoặc liệu đã bị hủy trước đó.")
                addToPending(match) -> ScanFeedback.Success(code, "Đã thêm vào danh sách chờ hủy.")
                else -> ScanFeedback.Success(code, "Đã có trong danh sách chờ.")
            }
        }
    }

    suspend fun saveBatch() {
        isSaving = true
        val toSave = pendingDispose.filter { it.status != PendingRowStatus.SAVING }
        for (row in toSave) {
            pendingDispose = pendingDispose.map { if (it.key == row.key) it.copy(status = PendingRowStatus.SAVING) else it }
            try {
                val resp = ApiClient.materialsApi.dispose(row.item.materialId, DisposeRequest(userId = session.userId))
                if (resp.isSuccessful) {
                    pendingDispose = pendingDispose.filterNot { it.key == row.key }
                } else {
                    val msg = resp.errorMessageOrDefault("Hủy liệu thất bại.")
                    pendingDispose = pendingDispose.map { if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = msg) else it }
                }
            } catch (e: Exception) {
                pendingDispose = pendingDispose.map {
                    if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = "Lỗi mạng: ${e.message}") else it
                }
            }
        }
        isSaving = false
        showConfirm = false
        loadDisposable()
    }

    HuyLieuScreenContent(
        pendingDispose = pendingDispose,
        isSaving = isSaving,
        showConfirm = showConfirm,
        showCamera = showCamera,
        lastFeedback = lastFeedback,
        snackbarHostState = snackbarHostState,
        onBack = onBack,
        onOpenCamera = { showCamera = true },
        onCloseCamera = { showCamera = false },
        onScan = { code -> scanChannel.trySend(code) },
        onRemovePending = { key -> pendingDispose = pendingDispose.filterNot { it.key == key } },
        onRequestSave = { showConfirm = true },
        onDismissConfirm = { if (!isSaving) showConfirm = false },
        onConfirmSave = { scope.launch { saveBatch() } },
        onDismissFeedback = { lastFeedback = null },
    )
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun HuyLieuScreenContent(
    pendingDispose: List<DisposePendingRow>,
    isSaving: Boolean,
    showConfirm: Boolean,
    showCamera: Boolean,
    lastFeedback: ScanFeedback?,
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onOpenCamera: () -> Unit,
    onCloseCamera: () -> Unit,
    onScan: (String) -> Unit,
    onRemovePending: (Long) -> Unit,
    onRequestSave: () -> Unit,
    onDismissConfirm: () -> Unit,
    onConfirmSave: () -> Unit,
    onDismissFeedback: () -> Unit,
) {
    var viewingDetail by remember { mutableStateOf<MaterialListItem?>(null) }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Hủy liệu") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Quay lại")
                    }
                },
                actions = {
                    IconButton(onClick = onOpenCamera) {
                        Icon(Icons.Filled.CameraAlt, contentDescription = "Quét bằng camera")
                    }
                },
            )
        },
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding)) {
            DataWedgeScanField(
                onScan = onScan,
                enabled = !showConfirm,
                modifier = Modifier.fillMaxWidth().padding(12.dp),
            )

            ScanFeedbackBanner(feedback = lastFeedback, onDismiss = onDismissFeedback)

            if (pendingDispose.isNotEmpty()) {
                Text(
                    "Đang chờ lưu (${pendingDispose.size})",
                    style = MaterialTheme.typography.titleSmall,
                    modifier = Modifier.padding(horizontal = 12.dp),
                )
                PendingBatchList(
                    rows = pendingDispose,
                    onRemove = onRemovePending,
                    modifier = Modifier.fillMaxWidth().height(200.dp),
                    onRowClick = { row -> viewingDetail = row.item },
                ) { row ->
                    Text(row.item.barcode, style = MaterialTheme.typography.titleMedium)
                    Text(
                        listOfNotNull(row.item.dev, row.item.matlDescription, row.item.status).joinToString(" / "),
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                Button(
                    onClick = onRequestSave,
                    enabled = pendingDispose.isNotEmpty() && !isSaving,
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error),
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp),
                ) {
                    Text("Lưu — hủy ${pendingDispose.size} liệu")
                }
                Spacer(Modifier.height(12.dp))
                HorizontalDivider()
            } else {
                Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                    Text(
                        "Quét mã để thêm liệu cần hủy vào danh sách chờ.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    )
                }
            }
        }
    }

    if (showCamera) {
        ContinuousBarcodeScannerDialog(
            onBarcodeScanned = onScan,
            onClose = onCloseCamera,
            lastFeedback = lastFeedback,
            onDismissFeedback = onDismissFeedback,
        )
    }

    if (showConfirm) {
        AlertDialog(
            onDismissRequest = onDismissConfirm,
            title = { Text("Xác nhận hủy liệu") },
            text = {
                Text(
                    "Hủy ${pendingDispose.size} liệu: " +
                        pendingDispose.joinToString(", ") { it.item.barcode } +
                        " — thao tác này KHÔNG thể hoàn tác.",
                )
            },
            confirmButton = {
                Button(
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error),
                    enabled = !isSaving,
                    onClick = onConfirmSave,
                ) {
                    Text(if (isSaving) "Đang xử lý..." else "Hủy liệu")
                }
            },
            dismissButton = {
                TextButton(enabled = !isSaving, onClick = onDismissConfirm) { Text("Thôi") }
            },
        )
    }

    viewingDetail?.let { item ->
        DisposeDetailDialog(item = item, onDismiss = { viewingDetail = null })
    }
}

/** Xem chi tiết 1 liệu trong danh sách chờ hủy — công nhân dùng để kiểm tra lại khi nghi ngờ quét
 *  nhầm, trước khi bấm Lưu (thao tác hủy không thể hoàn tác). */
@Composable
private fun DisposeDetailDialog(item: MaterialListItem, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = onDismiss) { Text("Đóng") } },
        title = { Text(item.barcode, style = MaterialTheme.typography.titleLarge) },
        text = {
            Column {
                InfoRow("Trạng thái", item.status)
                InfoRow("Vị trí", item.locationCode ?: "—")
                InfoRow("Dev", item.dev ?: "—")
                InfoRow("PO", item.poNo ?: "—")
                InfoRow("Mô tả", item.matlDescription ?: "—")
                InfoRow("Còn lại", "${item.balance ?: 0} ${item.unit ?: ""}")
            }
        },
    )
}

private val samplePendingDispose = listOf(
    DisposePendingRow(
        key = 1,
        item = MaterialListItem(materialId = 1, barcode = "QATEST001", dev = "QA/BUGTEST", matlDescription = "Vải lót", balance = 3.0, unit = "M", status = "InStock"),
    ),
)

@Preview(showBackground = true, name = "Chưa quét gì")
@Composable
private fun HuyLieuScreenEmptyPreview() {
    PmcWhAndroidTheme {
        HuyLieuScreenContent(
            pendingDispose = emptyList(),
            isSaving = false,
            showConfirm = false,
            showCamera = false,
            lastFeedback = ScanFeedback.Failure("QATEST999", "Không tìm thấy mã này, hoặc liệu đã bị hủy trước đó."),
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {},
            onRemovePending = {}, onRequestSave = {}, onDismissConfirm = {}, onConfirmSave = {},
            onDismissFeedback = {},
        )
    }
}

@Preview(showBackground = true, name = "Đang chờ lưu + xác nhận")
@Composable
private fun HuyLieuScreenConfirmPreview() {
    PmcWhAndroidTheme {
        HuyLieuScreenContent(
            pendingDispose = samplePendingDispose,
            isSaving = false,
            showConfirm = true,
            showCamera = false,
            lastFeedback = ScanFeedback.Success("QATEST001", "Đã thêm vào danh sách chờ hủy."),
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {},
            onRemovePending = {}, onRequestSave = {}, onDismissConfirm = {}, onConfirmSave = {},
            onDismissFeedback = {},
        )
    }
}
