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
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.OutlinedTextField
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.ReturnRequest
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
import com.samho.pmcwhandroid.ui.components.rememberExitConfirm
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch
import kotlinx.serialization.Serializable

@Serializable
private data class NhanLaiPendingItem(
    override val key: Long,
    val material: MaterialListItem,
    val qtyText: String = "",
    val chosenLocation: StorageLocationDto? = null,
    override val status: PendingRowStatus = PendingRowStatus.PENDING,
    override val error: String? = null,
) : PendingRow

// Trừ 2 số Double dễ dính lỗi làm tròn nhị phân (VD 37.8 - 11 = 26.799999999999997) — làm tròn
// 3 chữ số thập phân để hiện đẹp và để so sánh isValid() không bị lệch bởi phần dư li ti đó.
private fun roundQty(value: Double): Double = kotlin.math.round(value * 1000) / 1000.0

private fun NhanLaiPendingItem.outstanding(): Double = roundQty((material.arrivalQty ?: 0.0) - (material.balance ?: 0.0))
private fun NhanLaiPendingItem.parsedQty(): Double? = qtyText.replace(',', '.').toDoubleOrNull()
/** Liệu đã rời kệ hẳn (IssuedOut, hết sạch) — không còn vị trí nào, bắt buộc chọn kệ mới để lên
 *  lại. PartiallyIssued (chưa từng rời kệ) thì giữ nguyên locationCode cũ, không cần chọn. */
private fun NhanLaiPendingItem.needsLocation(): Boolean = material.locationCode == null
private fun NhanLaiPendingItem.isValid(): Boolean {
    val qty = parsedQty() ?: return false
    if (qty <= 0 || qty > outstanding()) return false
    if (needsLocation() && chosenLocation == null) return false
    return true
}

/**
 * Nhận lại hàng đã xuất (PMC feedback: bổ sung mục "Nhận lại"): quét liên tục, mỗi liệu quét được
 * kiểm tra đang IssuedOut/PartiallyIssued và còn số lượng đã xuất > 0 rồi thêm vào danh sách chờ,
 * điền sẵn số lượng = toàn bộ đã xuất (thường nhận lại hết, chỉnh tay nếu chỉ nhận lại 1 phần).
 * Liệu chưa từng rời kệ (PartiallyIssued, còn locationCode) tự giữ nguyên vị trí; liệu đã rời kệ
 * hẳn (IssuedOut) bắt buộc chọn kệ mới trước khi lưu được dòng đó — xem NhanLaiPendingItem.isValid().
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NhanLaiScreen(session: UserSession, onBack: () -> Unit) {
    // rememberSaveable: giữ lô đang quét dở qua process-death (Android giết app nền).
    var pending by rememberSaveable(stateSaver = listJsonSaver(NhanLaiPendingItem.serializer())) {
        mutableStateOf<List<NhanLaiPendingItem>>(emptyList())
    }
    val (guardedBack, exitConfirmDialog) = rememberExitConfirm(hasPendingWork = pending.isNotEmpty(), onExit = onBack)
    BackHandler(onBack = guardedBack)

    var locations by remember { mutableStateOf<List<StorageLocationDto>>(emptyList()) }
    var isLoadingLocations by remember { mutableStateOf(false) }
    var pickingLocationForKey by remember { mutableStateOf<Long?>(null) }
    var isSaving by remember { mutableStateOf(false) }
    var showCamera by remember { mutableStateOf(false) }
    var lastFeedback by remember { mutableStateOf<ScanFeedback?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    val scanChannel = remember { Channel<String>(Channel.UNLIMITED) }

    fun ensureLocationsLoaded() {
        if (locations.isNotEmpty() || isLoadingLocations) return
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

    fun addToPending(item: MaterialListItem): Boolean {
        if (pending.any { it.material.materialId == item.materialId }) return false
        val outstanding = roundQty((item.arrivalQty ?: 0.0) - (item.balance ?: 0.0))
        // PMC feedback: liệu đã rời kệ hẳn (locationCode == null) mà biết kệ cũ thì điền sẵn luôn kệ cũ
        // vào ô — công nhân thường lên lại đúng kệ cũ, khỏi phải mở picker chọn tay. Vẫn đổi được.
        val prefillLocation =
            if (item.locationCode == null && item.previousLocationId != null)
                StorageLocationDto(item.previousLocationId, 0, 0, item.previousLocationCode ?: "?")
            else null
        pending = pending + NhanLaiPendingItem(
            key = System.nanoTime(), material = item, qtyText = outstanding.toString(), chosenLocation = prefillLocation,
        )
        if (item.locationCode == null) ensureLocationsLoaded()
        return true
    }

    LaunchedEffect(Unit) {
        for (code in scanChannel) {
            try {
                val resp = ApiClient.materialsApi.getByBarcode(code)
                val item = resp.body()
                // roundQty giống addToPending / outstanding() — tránh bụi số thực (vd 1e-13) khiến
                // liệu đã nhận lại hết vẫn lọt vào danh sách chờ với qty "0.0" rồi kẹt không lưu được.
                val outstanding = roundQty((item?.arrivalQty ?: 0.0) - (item?.balance ?: 0.0))
                val canReturn = item != null && (item.status == "IssuedOut" || item.status == "PartiallyIssued") && outstanding > 0
                lastFeedback = when {
                    !resp.isSuccessful || item == null ->
                        ScanFeedback.Failure(code, resp.errorMessageOrDefault("Không tìm thấy mã '$code'."))
                    !canReturn ->
                        ScanFeedback.Failure(code, "Đang '${item.status}', không có gì để nhận lại.")
                    addToPending(item) -> ScanFeedback.Success(code, "Đã thêm vào danh sách chờ.")
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
        var savedCount = 0
        for (row in toSave) {
            val qty = row.parsedQty() ?: continue
            pending = pending.map { if (it.key == row.key) it.copy(status = PendingRowStatus.SAVING) else it }
            try {
                val resp = ApiClient.materialsApi.returnMaterial(
                    row.material.materialId,
                    ReturnRequest(locationId = row.chosenLocation?.locationId, qty = qty, userId = session.userId),
                )
                if (resp.isSuccessful) {
                    pending = pending.filterNot { it.key == row.key }
                    savedCount++
                } else {
                    val msg = resp.errorMessageOrDefault("Nhận lại thất bại.")
                    pending = pending.map { if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = msg) else it }
                }
            } catch (e: Exception) {
                pending = pending.map {
                    if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = "Lỗi mạng: ${e.message}") else it
                }
            }
        }
        isSaving = false
        // Màn hình phải "sạch" ngay sau khi lưu để nhận tiếp lô khác — xem giải thích ở XuatKhoScreen.
        lastFeedback = null
        if (savedCount > 0) {
            snackbarHostState.showSnackbar("Đã nhận lại xong $savedCount liệu — quét tiếp được ngay.")
        }
    }

    NhanLaiScreenContent(
        pending = pending,
        isSaving = isSaving,
        showCamera = showCamera,
        lastFeedback = lastFeedback,
        snackbarHostState = snackbarHostState,
        onBack = guardedBack,
        onOpenCamera = { showCamera = true },
        onCloseCamera = { showCamera = false },
        onScan = { code -> scanChannel.trySend(code) },
        scanEnabled = pickingLocationForKey == null,
        onQtyChange = { key, text -> pending = pending.map { if (it.key == key) it.copy(qtyText = text) else it } },
        onRemovePending = { key -> pending = pending.filterNot { it.key == key } },
        onChooseLocation = { key -> pickingLocationForKey = key; ensureLocationsLoaded() },
        onUsePreviousLocation = { key ->
            pending = pending.map { row ->
                val prevId = row.material.previousLocationId
                if (row.key == key && prevId != null) {
                    row.copy(chosenLocation = StorageLocationDto(prevId, 0, 0, row.material.previousLocationCode ?: "?"))
                } else {
                    row
                }
            }
        },
        onKeepOriginalLocation = { key ->
            pending = pending.map { if (it.key == key) it.copy(chosenLocation = null) else it }
        },
        onSave = { scope.launch { saveBatch() } },
        onDismissFeedback = { lastFeedback = null },
    )

    if (pickingLocationForKey != null) {
        LocationPickerDialog(
            locations = locations,
            isLoading = isLoadingLocations,
            onDismiss = { pickingLocationForKey = null },
            onSelect = { loc ->
                val key = pickingLocationForKey
                pending = pending.map { if (it.key == key) it.copy(chosenLocation = loc) else it }
                pickingLocationForKey = null
            },
        )
    }

    exitConfirmDialog()
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun NhanLaiScreenContent(
    pending: List<NhanLaiPendingItem>,
    isSaving: Boolean,
    showCamera: Boolean,
    lastFeedback: ScanFeedback?,
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onOpenCamera: () -> Unit,
    onCloseCamera: () -> Unit,
    onScan: (String) -> Unit,
    scanEnabled: Boolean = true,
    onQtyChange: (Long, String) -> Unit,
    onRemovePending: (Long) -> Unit,
    onChooseLocation: (Long) -> Unit,
    onUsePreviousLocation: (Long) -> Unit,
    onKeepOriginalLocation: (Long) -> Unit,
    onSave: () -> Unit,
    onDismissFeedback: () -> Unit,
) {
    var viewingDetail by remember { mutableStateOf<MaterialListItem?>(null) }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Nhận lại") },
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
                // Tắt khi đang chọn kệ (LocationPickerDialog) hoặc xem chi tiết liệu — không thì
                // phím từ súng quét rơi vào field ẩn sau dialog.
                enabled = scanEnabled && viewingDetail == null,
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
                    modifier = Modifier.fillMaxWidth().weight(1f, fill = false).heightIn(max = 400.dp),
                    onRowClick = { row -> viewingDetail = row.material },
                ) { row ->
                    Text(row.material.barcode, style = MaterialTheme.typography.titleMedium)
                    Text(
                        listOfNotNull(row.material.dev, row.material.model).joinToString(" / "),
                        style = MaterialTheme.typography.bodySmall,
                    )
                    Text(
                        listOfNotNull(row.material.matlDescription, row.material.colorCode).joinToString(" · "),
                        style = MaterialTheme.typography.bodySmall,
                    )
                    Spacer(Modifier.height(4.dp))
                    OutlinedTextField(
                        value = row.qtyText,
                        onValueChange = { onQtyChange(row.key, it) },
                        label = { Text("Số lượng nhận lại (đã xuất ${row.outstanding()})") },
                        singleLine = true,
                        isError = row.qtyText.isNotBlank() && !row.isValid(),
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Spacer(Modifier.height(4.dp))
                    if (row.needsLocation()) {
                        val prevId = row.material.previousLocationId
                        val prevCode = row.material.previousLocationCode
                        val chosen = row.chosenLocation
                        val usingPrev = prevId != null && chosen?.locationId == prevId
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            if (usingPrev) {
                                // PMC feedback: xuất hết rồi nhận lại thì ƯU TIÊN lên lại đúng kệ cũ —
                                // điền sẵn + hiện như mặc định, chỉ cần "Đổi" khi thật sự muốn kệ khác.
                                Text(
                                    "Kệ cũ: ${chosen!!.code} — lên lại đúng chỗ cũ",
                                    modifier = Modifier.weight(1f),
                                    style = MaterialTheme.typography.bodySmall,
                                    color = MaterialTheme.colorScheme.primary,
                                )
                                OutlinedButton(onClick = { onChooseLocation(row.key) }) { Text("Đổi") }
                            } else {
                                OutlinedButton(
                                    onClick = { onChooseLocation(row.key) },
                                    modifier = Modifier.weight(1f),
                                ) {
                                    Text(chosen?.let { "Kệ: ${it.code}" } ?: "Chọn kệ để lên lại (đã rời kệ hẳn)")
                                }
                                // Đã chọn/đổi sang kệ khác mà vẫn biết kệ cũ -> nút quay lại kệ cũ nhanh.
                                if (prevId != null) {
                                    OutlinedButton(onClick = { onUsePreviousLocation(row.key) }) {
                                        Text("Kệ cũ" + (prevCode?.let { " $it" } ?: ""))
                                    }
                                }
                            }
                        }
                    } else {
                        // Liệu chưa từng rời kệ (PartiallyIssued) — mặc định giữ nguyên kệ cũ, nhưng
                        // PMC feedback: cho nút "Thay đổi" phòng khi cần dời sang kệ khác lúc nhận lại.
                        val movedTo = row.chosenLocation
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            Text(
                                if (movedTo != null) "Kệ mới: ${movedTo.code} (dời từ ${row.material.locationCode})"
                                else "Kệ: ${row.material.locationCode} (giữ nguyên)",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.onSurfaceVariant,
                                modifier = Modifier.weight(1f),
                            )
                            if (movedTo != null) {
                                OutlinedButton(onClick = { onKeepOriginalLocation(row.key) }) { Text("Hoàn tác") }
                            } else {
                                OutlinedButton(onClick = { onChooseLocation(row.key) }) { Text("Thay đổi") }
                            }
                        }
                    }
                }
                Button(
                    onClick = onSave,
                    enabled = pending.isNotEmpty() && pending.all { it.isValid() } && !isSaving,
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
                ) {
                    Text(if (isSaving) "Đang lưu..." else "Lưu — nhận lại ${pending.size} liệu")
                }
            } else {
                Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                    Text(
                        "Quét mã để thêm liệu cần nhận lại vào danh sách chờ.",
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

    viewingDetail?.let { item ->
        MaterialDetailDialog(item = item, onDismiss = { viewingDetail = null })
    }
}

private val sampleReturnableItems = listOf(
    MaterialListItem(materialId = 1, barcode = "QATEST001", dev = "QA/BUGTEST", model = "Model X", arrivalQty = 30.0, balance = 10.0, unit = "PCS", status = "PartiallyIssued", locationCode = "3.2"),
    MaterialListItem(materialId = 2, barcode = "QATEST002", dev = "QA/BUGTEST", model = "Model Y", arrivalQty = 8.0, balance = 0.0, unit = "PCS", status = "IssuedOut", previousLocationId = 99, previousLocationCode = "34.1"),
)

private val samplePending = listOf(
    NhanLaiPendingItem(key = 1, material = sampleReturnableItems[0], qtyText = "20"),
    NhanLaiPendingItem(key = 2, material = sampleReturnableItems[1], qtyText = "8"),
)

@Preview(showBackground = true, name = "Chưa quét gì")
@Composable
private fun NhanLaiScreenEmptyPreview() {
    PmcWhAndroidTheme {
        NhanLaiScreenContent(
            pending = emptyList(),
            isSaving = false,
            showCamera = false,
            lastFeedback = null,
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {},
            onQtyChange = { _, _ -> }, onRemovePending = {}, onChooseLocation = {}, onUsePreviousLocation = {},
            onKeepOriginalLocation = {}, onSave = {}, onDismissFeedback = {},
        )
    }
}

@Preview(showBackground = true, name = "Đang chờ lưu — 1 giữ nguyên kệ, 1 cần chọn kệ mới")
@Composable
private fun NhanLaiScreenPendingPreview() {
    PmcWhAndroidTheme {
        NhanLaiScreenContent(
            pending = samplePending,
            isSaving = false,
            showCamera = false,
            lastFeedback = ScanFeedback.Success("QATEST001", "Đã thêm vào danh sách chờ."),
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {},
            onQtyChange = { _, _ -> }, onRemovePending = {}, onChooseLocation = {}, onUsePreviousLocation = {},
            onKeepOriginalLocation = {}, onSave = {}, onDismissFeedback = {},
        )
    }
}
