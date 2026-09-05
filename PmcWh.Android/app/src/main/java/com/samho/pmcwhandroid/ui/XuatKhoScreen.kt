package com.samho.pmcwhandroid.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
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
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.CameraAlt
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
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
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.IssueRequest
import com.samho.pmcwhandroid.network.MaterialListItem
import com.samho.pmcwhandroid.network.RecipientDto
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.ContinuousBarcodeScannerDialog
import com.samho.pmcwhandroid.scan.DataWedgeScanField
import com.samho.pmcwhandroid.scan.ScanFeedback
import com.samho.pmcwhandroid.scan.ScanFeedbackBanner
import com.samho.pmcwhandroid.ui.components.MaterialDetailDialog
import com.samho.pmcwhandroid.ui.components.listJsonSaver
import com.samho.pmcwhandroid.ui.components.nullableJsonSaver
import com.samho.pmcwhandroid.ui.components.rememberExitConfirm
import com.samho.pmcwhandroid.ui.components.PendingBatchList
import com.samho.pmcwhandroid.ui.components.PendingRow
import com.samho.pmcwhandroid.ui.components.PendingRowStatus
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.channels.Channel
import kotlinx.coroutines.launch
import kotlinx.serialization.Serializable

@Serializable
private data class XuatPendingItem(
    override val key: Long,
    val material: MaterialListItem,
    val qtyText: String = "",
    override val status: PendingRowStatus = PendingRowStatus.PENDING,
    override val error: String? = null,
) : PendingRow

private fun XuatPendingItem.parsedQty(): Double? = qtyText.replace(',', '.').toDoubleOrNull()

/**
 * Balance của liệu Staging LUÔN là 0 trong DB (chỉ được set = ArrivalQty lúc Nhập kho) — nên số
 * lượng "có thể xuất" của 1 liệu Staging phải lấy từ ArrivalQty, không phải Balance. Xem giải
 * thích/bug tương ứng đã sửa ở PmcWh.Api MaterialsController.Issue().
 */
private fun MaterialListItem.availableQtyForIssue(): Double =
    if (status == "Staging") (arrivalQty ?: 0.0) else (balance ?: 0.0)

/**
 * "Xuất Kho Thẳng": liệu Staging (chưa lên kệ) chỉ được xuất HẾT 1 lần, không cho xuất 1 phần —
 * xem giải thích ở PmcWh.Api MaterialsController.Issue(). InStock/PartiallyIssued thì xuất bao
 * nhiêu cũng được (miễn không vượt tồn) như trước giờ.
 */
private fun XuatPendingItem.isQtyValid(): Boolean {
    val qty = parsedQty() ?: return false
    val available = material.availableQtyForIssue()
    if (qty <= 0 || qty > available) return false
    if (material.status == "Staging" && qty < available) return false
    return true
}

/**
 * Chọn người nhận trước (dùng chung cho cả lô), rồi quét liên tục — mỗi liệu quét được thêm vào
 * danh sách chờ với 1 ô số lượng riêng (không thể chung 1 số lượng cho cả lô). "Lưu" duyệt qua
 * từng dòng hợp lệ, gọi issue() lần lượt. Cố ý KHÔNG hiện danh sách liệu nào để bấm chọn thủ công
 * — chỉ quét mới thêm được vào danh sách chờ, tránh công nhân bấm nhầm liệu (thao tác sai).
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun XuatKhoScreen(session: UserSession, onBack: () -> Unit) {
    // rememberSaveable: giữ lô đang quét dở + người nhận đã chọn qua process-death (Android giết app nền).
    var pending by rememberSaveable(stateSaver = listJsonSaver(XuatPendingItem.serializer())) {
        mutableStateOf<List<XuatPendingItem>>(emptyList())
    }
    val (guardedBack, exitConfirmDialog) = rememberExitConfirm(hasPendingWork = pending.isNotEmpty(), onExit = onBack)
    BackHandler(onBack = guardedBack)
    var recipients by remember { mutableStateOf<List<RecipientDto>>(emptyList()) }
    var isLoadingRecipients by remember { mutableStateOf(false) }
    var showRecipientPicker by remember { mutableStateOf(false) }
    var chosenRecipient by rememberSaveable(stateSaver = nullableJsonSaver(RecipientDto.serializer())) {
        mutableStateOf<RecipientDto?>(null)
    }
    // Đổi người nhận sẽ xoá sạch lô đang chờ — hỏi xác nhận nếu đang có việc dở (giống nút thoát).
    var showChangeRecipientConfirm by remember { mutableStateOf(false) }
    var isSaving by remember { mutableStateOf(false) }
    var showCamera by remember { mutableStateOf(false) }
    var lastFeedback by remember { mutableStateOf<ScanFeedback?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    // Gom cả 2 nguồn quét (DataWedge + camera) qua 1 channel, xử lý tuần tự từng mã một.
    val scanChannel = remember { Channel<String>(Channel.UNLIMITED) }

    fun openRecipientPicker() {
        showRecipientPicker = true
        if (recipients.isEmpty()) {
            isLoadingRecipients = true
            scope.launch {
                try {
                    recipients = ApiClient.recipientsApi.list().items
                } catch (e: Exception) {
                    snackbarHostState.showSnackbar("Không tải được người nhận: ${e.message}")
                } finally {
                    isLoadingRecipients = false
                }
            }
        }
    }

    fun addToPending(item: MaterialListItem): Boolean {
        if (pending.any { it.material.materialId == item.materialId }) return false
        // Staging (Xuất Kho Thẳng) chỉ được xuất hết 1 lần — điền sẵn luôn số lượng tồn cho đỡ gõ tay.
        val prefillQty = if (item.status == "Staging") item.availableQtyForIssue().toString() else ""
        pending = pending + XuatPendingItem(key = System.nanoTime(), material = item, qtyText = prefillQty)
        return true
    }

    LaunchedEffect(Unit) {
        for (code in scanChannel) {
            if (chosenRecipient == null) continue
            try {
                val resp = ApiClient.materialsApi.getByBarcode(code)
                val item = resp.body()
                // Staging = mới in tem, chưa lên kệ — cho xuất thẳng luôn ("Xuất Kho Thẳng"), không
                // bắt buộc phải Nhập kho trước. Xem PmcWh.Api MaterialsController.Issue().
                val canIssue = item != null &&
                    (item.status == "InStock" || item.status == "PartiallyIssued" || item.status == "Staging") &&
                    item.availableQtyForIssue() > 0
                lastFeedback = when {
                    !resp.isSuccessful || item == null ->
                        ScanFeedback.Failure(code, resp.errorMessageOrDefault("Không tìm thấy mã '$code'."))
                    !canIssue ->
                        ScanFeedback.Failure(code, "Đang '${item.status}' (tồn ${item.availableQtyForIssue()}), không thể xuất.")
                    addToPending(item) -> ScanFeedback.Success(code, "Đã thêm vào danh sách chờ.")
                    else -> ScanFeedback.Success(code, "Đã có trong danh sách chờ.")
                }
            } catch (e: Exception) {
                lastFeedback = ScanFeedback.Failure(code, "Lỗi mạng: ${e.message}")
            }
        }
    }

    suspend fun saveBatch() {
        val recipient = chosenRecipient ?: return
        isSaving = true
        val toSave = pending.filter { it.status != PendingRowStatus.SAVING }
        var savedCount = 0
        for (row in toSave) {
            val qty = row.parsedQty() ?: continue
            pending = pending.map { if (it.key == row.key) it.copy(status = PendingRowStatus.SAVING) else it }
            try {
                val resp = ApiClient.materialsApi.issue(
                    row.material.materialId,
                    IssueRequest(recipientId = recipient.recipientId, qty = qty, userId = session.userId),
                )
                if (resp.isSuccessful) {
                    pending = pending.filterNot { it.key == row.key }
                    savedCount++
                } else {
                    val msg = resp.errorMessageOrDefault("Xuất kho thất bại.")
                    pending = pending.map { if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = msg) else it }
                }
            } catch (e: Exception) {
                pending = pending.map {
                    if (it.key == row.key) it.copy(status = PendingRowStatus.ERROR, error = "Lỗi mạng: ${e.message}") else it
                }
            }
        }
        isSaving = false
        // PMC feedback: sau khi lưu xong màn hình phải "sạch" ngay để quét lô tiếp theo, không phải
        // tự back ra vào lại mới thấy hết đồ cũ — xoá banner quét cũ (không còn liên quan) + báo rõ
        // đã lưu xong bao nhiêu liệu (trước đây lưu xong mà không có gì báo, dễ tưởng bị đứng máy).
        lastFeedback = null
        if (savedCount > 0) {
            snackbarHostState.showSnackbar("Đã xuất xong $savedCount liệu — quét tiếp được ngay.")
        }
    }

    XuatKhoScreenContent(
        pending = pending,
        chosenRecipient = chosenRecipient,
        isSaving = isSaving,
        showCamera = showCamera,
        lastFeedback = lastFeedback,
        snackbarHostState = snackbarHostState,
        onBack = guardedBack,
        onOpenCamera = { showCamera = true },
        onCloseCamera = { showCamera = false },
        onScan = { code -> scanChannel.trySend(code) },
        onChooseRecipient = { openRecipientPicker() },
        onChangeRecipient = {
            if (pending.isEmpty()) chosenRecipient = null else showChangeRecipientConfirm = true
        },
        onQtyChange = { key, text -> pending = pending.map { if (it.key == key) it.copy(qtyText = text) else it } },
        onRemovePending = { key -> pending = pending.filterNot { it.key == key } },
        onSave = { scope.launch { saveBatch() } },
        onDismissFeedback = { lastFeedback = null },
    )

    if (showRecipientPicker) {
        RecipientPickerDialog(
            recipients = recipients,
            isLoading = isLoadingRecipients,
            onDismiss = { showRecipientPicker = false },
            onSelect = { r -> chosenRecipient = r; showRecipientPicker = false },
        )
    }

    if (showChangeRecipientConfirm) {
        AlertDialog(
            onDismissRequest = { showChangeRecipientConfirm = false },
            title = { Text("Đổi người nhận?") },
            text = { Text("Danh sách ${pending.size} liệu đang chờ lưu sẽ mất hết nếu đổi người nhận bây giờ.") },
            confirmButton = {
                Button(onClick = {
                    showChangeRecipientConfirm = false
                    chosenRecipient = null
                    pending = emptyList()
                }) { Text("Đổi") }
            },
            dismissButton = {
                TextButton(onClick = { showChangeRecipientConfirm = false }) { Text("Ở lại") }
            },
        )
    }

    exitConfirmDialog()
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun XuatKhoScreenContent(
    pending: List<XuatPendingItem>,
    chosenRecipient: RecipientDto?,
    isSaving: Boolean,
    showCamera: Boolean,
    lastFeedback: ScanFeedback?,
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onOpenCamera: () -> Unit,
    onCloseCamera: () -> Unit,
    onScan: (String) -> Unit,
    onChooseRecipient: () -> Unit,
    onChangeRecipient: () -> Unit,
    onQtyChange: (Long, String) -> Unit,
    onRemovePending: (Long) -> Unit,
    onSave: () -> Unit,
    onDismissFeedback: () -> Unit,
) {
    var viewingDetail by remember { mutableStateOf<MaterialListItem?>(null) }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Xuất kho") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Quay lại")
                    }
                },
                actions = {
                    if (chosenRecipient != null) {
                        IconButton(onClick = onOpenCamera) {
                            Icon(Icons.Filled.CameraAlt, contentDescription = "Quét bằng camera")
                        }
                    }
                },
            )
        },
    ) { padding ->
        if (chosenRecipient == null) {
            Column(modifier = Modifier.fillMaxSize().padding(padding)) {
                Card(
                    modifier = Modifier.fillMaxWidth().padding(12.dp),
                    onClick = onChooseRecipient,
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer),
                ) {
                    Row(
                        modifier = Modifier.fillMaxWidth().padding(16.dp),
                        horizontalArrangement = Arrangement.SpaceBetween,
                        verticalAlignment = Alignment.CenterVertically,
                    ) {
                        Text("Chọn người nhận để bắt đầu quét xuất kho", style = MaterialTheme.typography.titleMedium)
                        TextButton(onClick = onChooseRecipient) { Text("Chọn") }
                    }
                }
                Box(modifier = Modifier.fillMaxSize()) {
                    Text(
                        "Chọn người nhận rồi quét mã để xuất kho.",
                        color = MaterialTheme.colorScheme.onSurfaceVariant,
                        modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    )
                }
            }
        } else {
            Column(modifier = Modifier.fillMaxSize().padding(padding)) {
                Row(
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Text("Người nhận: ${chosenRecipient.name}", style = MaterialTheme.typography.titleMedium)
                    TextButton(onClick = onChangeRecipient) { Text("Đổi") }
                }

                DataWedgeScanField(
                    onScan = onScan,
                    // Tắt khi đang mở popup chi tiết liệu — không thì phím từ súng quét rơi vào field ẩn.
                    enabled = viewingDetail == null,
                    modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp),
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
                        row.material.matlDescription?.let {
                            Text(it, style = MaterialTheme.typography.bodySmall)
                        }
                        row.material.colorCode?.let {
                            Text("Color code: $it", style = MaterialTheme.typography.bodySmall)
                        }
                        if (row.material.status == "Staging") {
                            Text(
                                "Xuất Kho Thẳng — chưa lên kệ, chỉ xuất được hết toàn bộ số lượng",
                                style = MaterialTheme.typography.bodySmall,
                                color = MaterialTheme.colorScheme.primary,
                            )
                        }
                        Spacer(Modifier.height(4.dp))
                        Row(
                            modifier = Modifier.fillMaxWidth(),
                            horizontalArrangement = Arrangement.spacedBy(8.dp),
                            verticalAlignment = Alignment.CenterVertically,
                        ) {
                            OutlinedTextField(
                                value = row.qtyText,
                                onValueChange = { onQtyChange(row.key, it) },
                                label = { Text("Số lượng (tồn ${row.material.availableQtyForIssue()})") },
                                singleLine = true,
                                enabled = row.material.status != "Staging",
                                isError = row.qtyText.isNotBlank() && !row.isQtyValid(),
                                keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                                modifier = Modifier.weight(1f),
                            )
                            // PMC feedback: đỡ phải gõ tay — bấm "Xuất hết" tự điền toàn bộ tồn vào ô số lượng.
                            // Liệu Staging đã bị khoá ở mức đủ hết sẵn nên không cần nút này.
                            if (row.material.status != "Staging") {
                                OutlinedButton(
                                    onClick = {
                                        onQtyChange(row.key, row.material.availableQtyForIssue().toString())
                                    },
                                ) {
                                    Text("Xuất hết")
                                }
                            }
                        }
                    }
                    Button(
                        onClick = onSave,
                        enabled = pending.isNotEmpty() && pending.all { it.isQtyValid() } && !isSaving,
                        modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 8.dp),
                    ) {
                        Text(if (isSaving) "Đang lưu..." else "Lưu — xuất ${pending.size} liệu")
                    }
                } else {
                    Box(modifier = Modifier.weight(1f).fillMaxWidth()) {
                        Text(
                            "Quét mã để thêm liệu vào danh sách chờ.",
                            color = MaterialTheme.colorScheme.onSurfaceVariant,
                            modifier = Modifier.align(Alignment.Center).padding(24.dp),
                        )
                    }
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

@Composable
private fun RecipientPickerDialog(
    recipients: List<RecipientDto>,
    isLoading: Boolean,
    onDismiss: () -> Unit,
    onSelect: (RecipientDto) -> Unit,
) {
    var query by remember { mutableStateOf("") }
    val filtered = remember(recipients, query) {
        if (query.isBlank()) recipients else recipients.filter { it.name.contains(query, ignoreCase = true) }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = onDismiss) { Text("Đóng") } },
        title = { Text("Chọn người nhận") },
        text = {
            Column {
                OutlinedTextField(
                    value = query,
                    onValueChange = { query = it },
                    label = { Text("Tìm theo tên") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )
                Spacer(Modifier.height(8.dp))
                Box(modifier = Modifier.fillMaxWidth().height(320.dp)) {
                    if (isLoading) {
                        CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                    } else {
                        LazyColumn {
                            items(filtered, key = { it.recipientId }) { r ->
                                ListItem(
                                    headlineContent = { Text(r.name) },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .clickable { onSelect(r) },
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

private val sampleIssuableItems = listOf(
    MaterialListItem(materialId = 1, barcode = "QATEST001", dev = "QA/BUGTEST", model = "Model X", balance = 30.0, unit = "PCS", status = "InStock"),
    MaterialListItem(materialId = 2, barcode = "QATEST002", dev = "QA/BUGTEST", model = "Model Y", balance = 8.0, unit = "PCS", status = "PartiallyIssued"),
)

private val samplePending = listOf(
    XuatPendingItem(key = 1, material = sampleIssuableItems[0], qtyText = "5"),
    XuatPendingItem(key = 2, material = sampleIssuableItems[1], qtyText = ""),
)

@Preview(showBackground = true, name = "Chưa chọn người nhận")
@Composable
private fun XuatKhoScreenNoRecipientPreview() {
    PmcWhAndroidTheme {
        XuatKhoScreenContent(
            pending = emptyList(),
            chosenRecipient = null,
            isSaving = false,
            showCamera = false,
            lastFeedback = null,
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {}, onChooseRecipient = {},
            onChangeRecipient = {}, onQtyChange = { _, _ -> }, onRemovePending = {},
            onSave = {}, onDismissFeedback = {},
        )
    }
}

@Preview(showBackground = true, name = "Đang chờ lưu, nhập số lượng")
@Composable
private fun XuatKhoScreenPendingPreview() {
    PmcWhAndroidTheme {
        XuatKhoScreenContent(
            pending = samplePending,
            chosenRecipient = RecipientDto(recipientId = 1, name = "Xưởng May 1"),
            isSaving = false,
            showCamera = false,
            lastFeedback = ScanFeedback.Success("QATEST001", "Đã thêm vào danh sách chờ."),
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onOpenCamera = {}, onCloseCamera = {}, onScan = {}, onChooseRecipient = {},
            onChangeRecipient = {}, onQtyChange = { _, _ -> }, onRemovePending = {},
            onSave = {}, onDismissFeedback = {},
        )
    }
}
