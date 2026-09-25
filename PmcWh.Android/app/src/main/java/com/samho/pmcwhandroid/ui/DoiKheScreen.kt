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
import androidx.compose.foundation.layout.heightIn
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.RelocateRequest
import com.samho.pmcwhandroid.network.StorageLocationDto
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.ContinuousBarcodeScannerDialog
import com.samho.pmcwhandroid.scan.DataWedgeScanField
import com.samho.pmcwhandroid.scan.ScanFeedback
import com.samho.pmcwhandroid.scan.ScanFeedbackBanner
import com.samho.pmcwhandroid.ui.components.LocationPickerDialog
import com.samho.pmcwhandroid.ui.components.MaterialDetailDialog
import com.samho.pmcwhandroid.ui.components.PendingBatchList
import com.samho.pmcwhandroid.ui.components.PendingRow
import com.samho.pmcwhandroid.ui.components.PendingRowStatus
import com.samho.pmcwhandroid.ui.components.listJsonSaver
import com.samho.pmcwhandroid.ui.components.nullableJsonSaver
import com.samho.pmcwhandroid.ui.components.rememberExitConfirm
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch
import kotlinx.serialization.Serializable

/**
 * PMC feedback: liệu nhỏ (dây, chỉ, các loại liệu nhỏ...) cần dời qua kệ khác thường xuyên, không
 * gắn với nghiệp vụ Xuất/Nhận lại nào cả — Nhập kho chỉ nhận liệu Staging, Nhận lại đòi hỏi liệu
 * đang "đã xuất". Màn này chọn 1 kệ đích trước (giống Nhập kho), rồi quét theo lô để dời hàng loạt
 * liệu InStock/PartiallyIssued vào đúng kệ đó — gọi api/Materials/{id}/relocate (không đổi Balance/
 * Status, chỉ đổi CurrentLocationId). Chỉ có trên app mobile, không có trên Web (PMC yêu cầu).
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DoiKheScreen(session: UserSession, onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    // rememberSaveable: khôi phục đúng kệ đích sau process-death (lô quét dở của DoiKheBatchScreen
    // cũng tự khôi phục qua rememberSaveable riêng của nó).
    var targetLocation by rememberSaveable(stateSaver = nullableJsonSaver(StorageLocationDto.serializer())) {
        mutableStateOf<StorageLocationDto?>(null)
    }
    var locations by remember { mutableStateOf<List<StorageLocationDto>>(emptyList()) }
    var isLoadingLocations by remember { mutableStateOf(false) }
    var showLocationPicker by remember { mutableStateOf(false) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

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

    val location = targetLocation
    if (location != null) {
        DoiKheBatchScreen(
            session = session,
            location = location,
            onClose = { targetLocation = null },
            onAllSavedAndClosed = { targetLocation = null },
        )
    } else {
        DoiKheScreenContent(
            snackbarHostState = snackbarHostState,
            onBack = onBack,
            onChooseLocation = { openLocationPicker() },
        )
    }

    if (showLocationPicker) {
        LocationPickerDialog(
            locations = locations,
            isLoading = isLoadingLocations,
            onDismiss = { showLocationPicker = false },
            onSelect = { loc -> targetLocation = loc; showLocationPicker = false },
        )
    }
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DoiKheScreenContent(
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onChooseLocation: () -> Unit,
) {
    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Đổi kệ") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Quay lại")
                    }
                },
            )
        },
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding)) {
            Card(
                modifier = Modifier.fillMaxWidth().padding(12.dp),
                onClick = onChooseLocation,
                colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer),
            ) {
                Row(
                    modifier = Modifier.fillMaxWidth().padding(16.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text("Chọn kệ đích để quét dời liệu", style = MaterialTheme.typography.titleMedium)
                    TextButton(onClick = onChooseLocation) { Text("Chọn kệ") }
                }
            }

            Box(modifier = Modifier.fillMaxSize()) {
                Text(
                    "Chọn kệ đích rồi quét mã để dời liệu (dây, chỉ...) sang kệ đó.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                )
            }
        }
    }
}

@Preview(showBackground = true, name = "Chưa chọn kệ")
@Composable
private fun DoiKheScreenPreview() {
    PmcWhAndroidTheme {
        DoiKheScreenContent(
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {},
            onChooseLocation = {},
        )
    }
}

@Serializable
private data class DoiKhePendingRow(
    override val key: Long,
    val item: MaterialListItem,
    override val status: PendingRowStatus = PendingRowStatus.PENDING,
    override val error: String? = null,
) : PendingRow

/**
 * Luồng dùng chung sau khi đã chọn kệ đích ([location]): quét liên tục, mỗi mã resolve qua
 * getByBarcode(), hợp lệ khi đang InStock/PartiallyIssued (thật sự nằm trên 1 kệ) VÀ chưa ở đúng
 * kệ đích — "Lưu" duyệt qua danh sách gọi relocate() cho từng liệu. Cố ý KHÔNG hiện danh sách liệu
 * nào để bấm chọn thủ công — chỉ quét mới thêm được vào danh sách chờ, giống mọi màn quét theo lô
 * khác trong app.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DoiKheBatchScreen(
    session: UserSession,
    location: StorageLocationDto,
    onClose: () -> Unit,
    onAllSavedAndClosed: () -> Unit,
) {
    // rememberSaveable: giữ lô đang quét dở qua process-death (Android giết app nền).
    var pending by rememberSaveable(stateSaver = listJsonSaver(DoiKhePendingRow.serializer())) {
        mutableStateOf<List<DoiKhePendingRow>>(emptyList())
    }
    val (guardedClose, exitConfirmDialog) = rememberExitConfirm(hasPendingWork = pending.isNotEmpty(), onExit = onClose)
    BackHandler(onBack = guardedClose)
    var isSaving by remember { mutableStateOf(false) }
    var showCamera by remember { mutableStateOf(false) }
    var lastFeedback by remember { mutableStateOf<ScanFeedback?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    // Gom cả 2 nguồn quét (DataWedge + camera) qua 1 channel, xử lý tuần tự từng mã một.
    val scanChannel = remember { Channel<String>(Channel.UNLIMITED) }

    fun addItem(item: MaterialListItem): Boolean {
        if (pending.any { it.item.materialId == item.materialId }) return false
        pending = pending + DoiKhePendingRow(key = System.nanoTime(), item = item)
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
                    item.status != "InStock" && item.status != "PartiallyIssued" ->
                        ScanFeedback.Failure(code, "Đang '${item.status}', không thể đổi kệ.")
                    item.locationCode == location.code ->
                        ScanFeedback.Failure(code, "Đã ở đúng kệ này rồi.")
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
                val resp = ApiClient.materialsApi.relocate(
                    row.item.materialId,
                    RelocateRequest(locationId = location.locationId, userId = session.userId),
                )
                if (resp.isSuccessful) {
                    pending = pending.filterNot { it.key == row.key }
                } else {
                    val msg = resp.errorMessageOrDefault("Đổi kệ thất bại.")
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

    DoiKheBatchScreenContent(
        location = location,
        pending = pending,
        isSaving = isSaving,
        showCamera = showCamera,
        lastFeedback = lastFeedback,
        snackbarHostState = snackbarHostState,
        onClose = guardedClose,
        onOpenCamera = { showCamera = true },
        onCloseCamera = { showCamera = false },
        onScan = { code -> scanChannel.trySend(code) },
        onRemovePending = { key -> pending = pending.filterNot { it.key == key } },
        onSave = { scope.launch { saveBatch() } },
        onDismissFeedback = { lastFeedback = null },
    )

    exitConfirmDialog()
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DoiKheBatchScreenContent(
    location: StorageLocationDto,
    pending: List<DoiKhePendingRow>,
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
                title = { Text("Đổi kệ về ${location.code}") },
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
                // Tắt khi đang mở popup chi tiết liệu.
                enabled = viewingDetail == null,
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
                    Text(
                        listOfNotNull(row.item.matlDescription, row.item.colorCode).joinToString(" · "),
                        style = MaterialTheme.typography.bodySmall,
                    )
                    Text(
                        "Kệ hiện tại: ${row.item.locationCode ?: "—"}",
                        style = MaterialTheme.typography.bodySmall,
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                    )
                }
                Button(
                    onClick = onSave,
                    enabled = pending.isNotEmpty() && !isSaving,
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp),
                ) {
                    Text(if (isSaving) "Đang lưu..." else "Lưu — đổi ${pending.size} liệu sang kệ ${location.code}")
                }
                Spacer(Modifier.height(12.dp))
                HorizontalDivider()
            }

            Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                Text(
                    "Quét mã để thêm liệu cần đổi kệ vào danh sách chờ.",
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

private val sampleDoiKhePending = listOf(
    DoiKhePendingRow(key = 1, item = MaterialListItem(materialId = 1, barcode = "QATEST001", dev = "QA/BUGTEST", model = "Model X", balance = 10.0, unit = "PCS", status = "InStock", locationCode = "5.2")),
    DoiKhePendingRow(key = 2, item = MaterialListItem(materialId = 2, barcode = "QATEST002", dev = "QA/BUGTEST", model = "Model Y", balance = 5.0, unit = "PCS", status = "PartiallyIssued", locationCode = "6.3")),
)

private val sampleDoiKheLocation = StorageLocationDto(locationId = 1, rackNo = 1, levelNo = 1, code = "1.1")

@Preview(showBackground = true, name = "Chưa quét gì")
@Composable
private fun DoiKheBatchScreenEmptyPreview() {
    PmcWhAndroidTheme {
        DoiKheBatchScreenContent(
            location = sampleDoiKheLocation,
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
private fun DoiKheBatchScreenPendingPreview() {
    PmcWhAndroidTheme {
        DoiKheBatchScreenContent(
            location = sampleDoiKheLocation,
            pending = sampleDoiKhePending,
            isSaving = false,
            showCamera = false,
            lastFeedback = ScanFeedback.Success("QATEST002", "Đã thêm vào danh sách chờ."),
            snackbarHostState = remember { SnackbarHostState() },
            onClose = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {},
            onRemovePending = {}, onSave = {}, onDismissFeedback = {},
        )
    }
}
