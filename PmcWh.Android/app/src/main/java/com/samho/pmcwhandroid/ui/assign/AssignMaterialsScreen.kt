package com.samho.pmcwhandroid.ui.assign

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.Button
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
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.InboundRequest
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.StorageLocationDto
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.ContinuousBarcodeScannerDialog
import com.samho.pmcwhandroid.scan.DataWedgeScanField
import com.samho.pmcwhandroid.scan.ScanFeedback
import com.samho.pmcwhandroid.scan.ScanFeedbackBanner
import com.samho.pmcwhandroid.ui.components.MaterialDetailDialog
import com.samho.pmcwhandroid.ui.components.PendingBatchList
import com.samho.pmcwhandroid.ui.components.PendingRow
import com.samho.pmcwhandroid.ui.components.PendingRowStatus
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch

private data class AssignPendingRow(
    override val key: Long,
    val item: MaterialListItem,
    override val status: PendingRowStatus = PendingRowStatus.PENDING,
    override val error: String? = null,
) : PendingRow

/**
 * Luồng dùng chung: đã có sẵn 1 kệ đích ([location]), quét liên tục để gán các liệu Staging vào
 * kệ đó theo lô — mỗi mã quét được resolve qua getByBarcode(), nếu hợp lệ (status == "Staging")
 * thì thêm vào danh sách chờ; "Lưu" duyệt qua danh sách gọi inbound() cho từng liệu. Vào từ Nhập
 * Kho (đã chọn kệ qua LocationPickerDialog) hoặc Danh Sách Kệ (đã chọn qua tap tầng kệ).
 *
 * Cố ý KHÔNG có danh sách liệu nào để bấm chọn thủ công — chỉ quét mới thêm được vào danh sách
 * chờ, tránh công nhân bấm nhầm liệu trong 1 danh sách dài (thao tác sai).
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun AssignMaterialsScreen(
    session: UserSession,
    location: StorageLocationDto,
    onClose: () -> Unit,
    onAllSavedAndClosed: () -> Unit,
) {
    BackHandler(onBack = onClose)
    var pending by remember { mutableStateOf<List<AssignPendingRow>>(emptyList()) }
    var isSaving by remember { mutableStateOf(false) }
    var showCamera by remember { mutableStateOf(false) }
    var lastFeedback by remember { mutableStateOf<ScanFeedback?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    // Gom cả 2 nguồn quét (DataWedge + camera) qua 1 channel, xử lý tuần tự từng mã một.
    val scanChannel = remember { Channel<String>(Channel.UNLIMITED) }

    fun addItem(item: MaterialListItem): Boolean {
        if (pending.any { it.item.materialId == item.materialId }) return false
        pending = pending + AssignPendingRow(key = System.nanoTime(), item = item)
        return true
    }

    LaunchedEffect(Unit) {
        for (code in scanChannel) {
            try {
                val resp = ApiClient.materialsApi.getByBarcode(code)
                val item = resp.body()
                lastFeedback = when {
                    !resp.isSuccessful || item == null ->
                        ScanFeedback.Failure(code, resp.errorMessageOrDefault("Không tìm thấy mã '$code'."))
                    item.status != "Staging" ->
                        ScanFeedback.Failure(code, "Đang '${item.status}', không thể lên kệ.")
                    addItem(item) -> ScanFeedback.Success(code, "Đã thêm vào danh sách chờ.")
                    else -> ScanFeedback.Success(code, "Đã có trong danh sách chờ.")
                }
            } catch (e: Exception) {
                lastFeedback = ScanFeedback.Failure(code, "Lỗi mạng: ${e.message}")
            }
        }
    }

    suspend fun saveBatch() {
        isSaving = true
        val toSave = pending.filter { it.status != PendingRowStatus.SAVING }
        for (row in toSave) {
            pending = pending.map { if (it.key == row.key) it.copy(status = PendingRowStatus.SAVING) else it }
            try {
                val resp = ApiClient.materialsApi.inbound(
                    row.item.materialId,
                    InboundRequest(locationId = location.locationId, userId = session.userId),
                )
                if (resp.isSuccessful) {
                    pending = pending.filterNot { it.key == row.key }
                } else {
                    val msg = resp.errorMessageOrDefault("Nhập kho thất bại.")
                    pending = pending.map { if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = msg) else it }
                }
            } catch (e: Exception) {
                pending = pending.map {
                    if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = "Lỗi mạng: ${e.message}") else it
                }
            }
        }
        isSaving = false
        if (pending.isEmpty()) onAllSavedAndClosed()
    }

    AssignMaterialsScreenContent(
        location = location,
        pending = pending,
        isSaving = isSaving,
        showCamera = showCamera,
        lastFeedback = lastFeedback,
        snackbarHostState = snackbarHostState,
        onClose = onClose,
        onOpenCamera = { showCamera = true },
        onCloseCamera = { showCamera = false },
        onScan = { code -> scanChannel.trySend(code) },
        onRemovePending = { key -> pending = pending.filterNot { it.key == key } },
        onSave = { scope.launch { saveBatch() } },
        onDismissFeedback = { lastFeedback = null },
    )
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun AssignMaterialsScreenContent(
    location: StorageLocationDto,
    pending: List<AssignPendingRow>,
    isSaving: Boolean,
    showCamera: Boolean,
    lastFeedback: ScanFeedback?,
    snackbarHostState: SnackbarHostState,
    onClose: () -> Unit,
    onOpenCamera: () -> Unit,
    onCloseCamera: () -> Unit,
    onScan: (String) -> Unit,
    onRemovePending: (Long) -> Unit,
    onSave: () -> Unit,
    onDismissFeedback: () -> Unit,
) {
    var viewingDetail by remember { mutableStateOf<MaterialListItem?>(null) }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Gán liệu vào kệ ${location.code}") },
                navigationIcon = {
                    IconButton(onClick = onClose) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Đóng")
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
                modifier = Modifier.fillMaxWidth().padding(12.dp),
            )
            ScanFeedbackBanner(feedback = lastFeedback, onDismiss = onDismissFeedback)

            if (pending.isNotEmpty()) {
                Text(
                    "Đang chờ lưu (${pending.size})",
                    style = MaterialTheme.typography.titleSmall,
                    modifier = Modifier.padding(horizontal = 12.dp),
                )
                PendingBatchList(
                    rows = pending,
                    onRemove = onRemovePending,
                    modifier = Modifier.fillMaxWidth().heightIn(max = 260.dp),
                    onRowClick = { row -> viewingDetail = row.item },
                ) { row ->
                    Text(row.item.barcode, style = MaterialTheme.typography.titleMedium)
                    Text(
                        listOfNotNull(row.item.dev, row.item.model).joinToString(" / "),
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                Button(
                    onClick = onSave,
                    enabled = pending.isNotEmpty() && !isSaving,
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp),
                ) {
                    Text(if (isSaving) "Đang lưu..." else "Lưu — ${pending.size} liệu vào kệ ${location.code}")
                }
                Spacer(Modifier.height(12.dp))
                HorizontalDivider()
            }

            Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                Text(
                    "Quét mã để thêm liệu vào danh sách chờ.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                )
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

    viewingDetail?.let { item ->
        MaterialDetailDialog(item = item, onDismiss = { viewingDetail = null })
    }
}

private val samplePending = listOf(
    AssignPendingRow(key = 1, item = MaterialListItem(materialId = 1, barcode = "QATEST001", dev = "QA/BUGTEST", model = "Model X", arrivalQty = 10.0, unit = "PCS", status = "Staging")),
    AssignPendingRow(key = 2, item = MaterialListItem(materialId = 2, barcode = "QATEST002", dev = "QA/BUGTEST", model = "Model Y", arrivalQty = 5.0, unit = "PCS", status = "Staging")),
)

private val sampleLocation = StorageLocationDto(locationId = 1, rackNo = 1, levelNo = 1, code = "1.1")

@Preview(showBackground = true, name = "Chưa quét gì")
@Composable
private fun AssignMaterialsScreenEmptyPreview() {
    PmcWhAndroidTheme {
        AssignMaterialsScreenContent(
            location = sampleLocation,
            pending = emptyList(),
            isSaving = false,
            showCamera = false,
            lastFeedback = null,
            snackbarHostState = remember { SnackbarHostState() },
            onClose = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {},
            onRemovePending = {}, onSave = {}, onDismissFeedback = {},
        )
    }
}

@Preview(showBackground = true, name = "Đang chờ lưu")
@Composable
private fun AssignMaterialsScreenPendingPreview() {
    PmcWhAndroidTheme {
        AssignMaterialsScreenContent(
            location = sampleLocation,
            pending = samplePending,
            isSaving = false,
            showCamera = false,
            lastFeedback = ScanFeedback.Success("QATEST002", "Đã thêm vào danh sách chờ."),
            snackbarHostState = remember { SnackbarHostState() },
            onClose = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {},
            onRemovePending = {}, onSave = {}, onDismissFeedback = {},
        )
    }
}
