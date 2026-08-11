package com.samho.pmcwhandroid.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
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
import com.samho.pmcwhandroid.scan.rememberBarcodeScanner
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.Job
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun XuatKhoScreen(session: UserSession, onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    var issuableItems by remember { mutableStateOf<List<MaterialListItem>>(emptyList()) }
    var isLoadingList by remember { mutableStateOf(true) }
    var selected by remember { mutableStateOf<MaterialListItem?>(null) }
    var qtyText by remember { mutableStateOf("") }
    var recipients by remember { mutableStateOf<List<RecipientDto>>(emptyList()) }
    var isLoadingRecipients by remember { mutableStateOf(false) }
    var showRecipientPicker by remember { mutableStateOf(false) }
    var chosenRecipient by remember { mutableStateOf<RecipientDto?>(null) }
    var isSubmitting by remember { mutableStateOf(false) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()
    // Quét liên tục có thể bắn nhiều lookup cùng lúc — hủy lookup cũ trước khi quét mới, tránh
    // response cũ về sau đổi ngầm "selected" sang liệu khác lúc người dùng đang xác nhận.
    var scanJob by remember { mutableStateOf<Job?>(null) }

    suspend fun loadIssuable() {
        isLoadingList = true
        try {
            issuableItems = ApiClient.materialsApi.issuable()
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Không tải được danh sách: ${e.message}")
        } finally {
            isLoadingList = false
        }
    }

    LaunchedEffect(Unit) { loadIssuable() }

    fun selectMaterial(item: MaterialListItem) {
        selected = item
        qtyText = ""
        chosenRecipient = null
    }

    val scanLauncher = rememberBarcodeScanner(
        onResult = { code ->
            scanJob?.cancel()
            scanJob = scope.launch {
                try {
                    val resp = ApiClient.materialsApi.getByBarcode(code)
                    val item = resp.body()
                    if (resp.isSuccessful && item != null) {
                        val canIssue = (item.status == "InStock" || item.status == "PartiallyIssued") && (item.balance ?: 0.0) > 0
                        if (canIssue) {
                            selectMaterial(item)
                        } else {
                            snackbarHostState.showSnackbar(
                                "Liệu '${item.barcode}' đang '${item.status}' (tồn ${item.balance ?: 0}), không thể xuất.",
                            )
                        }
                    } else {
                        snackbarHostState.showSnackbar(resp.errorMessageOrDefault("Không tìm thấy mã '$code'."))
                    }
                } catch (e: Exception) {
                    snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
                }
            }
        },
        onError = { msg -> scope.launch { snackbarHostState.showSnackbar(msg) } },
    )

    val qty = qtyText.replace(',', '.').toDoubleOrNull()
    val balance = selected?.balance ?: 0.0
    val qtyValid = qty != null && qty > 0 && qty <= balance

    suspend fun submit() {
        val material = selected ?: return
        val recipient = chosenRecipient ?: return
        val q = qty ?: return
        isSubmitting = true
        try {
            val resp = ApiClient.materialsApi.issue(
                material.materialId,
                IssueRequest(recipientId = recipient.recipientId, qty = q, userId = session.userId),
            )
            if (resp.isSuccessful) {
                snackbarHostState.showSnackbar("Đã xuất ${material.barcode}: $q ${material.unit ?: ""} cho ${recipient.name}")
                selected = null
                chosenRecipient = null
                qtyText = ""
                loadIssuable()
            } else {
                snackbarHostState.showSnackbar(resp.errorMessageOrDefault("Xuất kho thất bại."))
            }
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
        } finally {
            isSubmitting = false
        }
    }

    XuatKhoScreenContent(
        issuableItems = issuableItems,
        isLoadingList = isLoadingList,
        selected = selected,
        qtyText = qtyText,
        qtyValid = qtyValid,
        balance = balance,
        chosenRecipient = chosenRecipient,
        isSubmitting = isSubmitting,
        snackbarHostState = snackbarHostState,
        onBack = onBack,
        onScan = scanLauncher,
        onSelectMaterial = { item -> selectMaterial(item) },
        onQtyChange = { qtyText = it },
        onOpenRecipientPicker = {
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
        },
        onSubmit = { scope.launch { submit() } },
        onChooseAnother = { selected = null; chosenRecipient = null; qtyText = "" },
    )

    if (showRecipientPicker) {
        RecipientPickerDialog(
            recipients = recipients,
            isLoading = isLoadingRecipients,
            onDismiss = { showRecipientPicker = false },
            onSelect = { r -> chosenRecipient = r; showRecipientPicker = false },
        )
    }
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun XuatKhoScreenContent(
    issuableItems: List<MaterialListItem>,
    isLoadingList: Boolean,
    selected: MaterialListItem?,
    qtyText: String,
    qtyValid: Boolean,
    balance: Double,
    chosenRecipient: RecipientDto?,
    isSubmitting: Boolean,
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onScan: () -> Unit,
    onSelectMaterial: (MaterialListItem) -> Unit,
    onQtyChange: (String) -> Unit,
    onOpenRecipientPicker: () -> Unit,
    onSubmit: () -> Unit,
    onChooseAnother: () -> Unit,
) {
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
                    IconButton(onClick = onScan) {
                        Icon(Icons.Filled.QrCodeScanner, contentDescription = "Quét mã")
                    }
                },
            )
        },
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            if (selected == null) {
                when {
                    isLoadingList -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                    issuableItems.isEmpty() -> Text(
                        "Không có liệu nào có thể xuất.",
                        modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    )
                    else -> LazyColumn(modifier = Modifier.fillMaxSize()) {
                        items(issuableItems, key = { it.materialId }) { item ->
                            Card(
                                modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp),
                                onClick = { onSelectMaterial(item) },
                            ) {
                                ListItem(
                                    headlineContent = { Text(item.barcode) },
                                    supportingContent = {
                                        Text(listOfNotNull(item.dev, item.model).joinToString(" / "))
                                    },
                                    trailingContent = {
                                        Text("Tồn: ${item.balance ?: 0} ${item.unit ?: ""}")
                                    },
                                )
                            }
                        }
                    }
                }
            } else {
                Column(modifier = Modifier.fillMaxSize().padding(16.dp)) {
                    Card(modifier = Modifier.fillMaxWidth()) {
                        Column(modifier = Modifier.padding(16.dp)) {
                            Text(selected.barcode, style = MaterialTheme.typography.titleLarge)
                            Text(
                                listOfNotNull(selected.dev, selected.model).joinToString(" / "),
                                style = MaterialTheme.typography.bodyMedium,
                            )
                            Text(
                                "Tồn hiện tại: ${selected.balance ?: 0} ${selected.unit ?: ""}",
                                style = MaterialTheme.typography.bodyMedium,
                            )
                        }
                    }

                    Spacer(Modifier.height(16.dp))

                    OutlinedTextField(
                        value = qtyText,
                        onValueChange = onQtyChange,
                        label = { Text("Số lượng xuất") },
                        singleLine = true,
                        isError = qtyText.isNotBlank() && !qtyValid,
                        supportingText = {
                            if (qtyText.isNotBlank() && !qtyValid) {
                                Text("Nhập số lớn hơn 0 và không vượt quá tồn ($balance)")
                            }
                        },
                        keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Decimal),
                        modifier = Modifier.fillMaxWidth(),
                    )

                    Spacer(Modifier.height(12.dp))

                    OutlinedButton(
                        modifier = Modifier.fillMaxWidth(),
                        onClick = onOpenRecipientPicker,
                    ) {
                        Text(chosenRecipient?.let { "Người nhận: ${it.name}" } ?: "Chọn người nhận")
                    }

                    Spacer(Modifier.height(12.dp))

                    Button(
                        modifier = Modifier.fillMaxWidth(),
                        enabled = qtyValid && chosenRecipient != null && !isSubmitting,
                        onClick = onSubmit,
                    ) {
                        Text(if (isSubmitting) "Đang xử lý..." else "Xác nhận xuất kho")
                    }

                    Spacer(Modifier.height(8.dp))

                    TextButton(
                        modifier = Modifier.fillMaxWidth(),
                        onClick = onChooseAnother,
                    ) {
                        Text("Chọn liệu khác")
                    }
                }
            }
        }
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

@Preview(showBackground = true, name = "Danh sách liệu")
@Composable
private fun XuatKhoScreenListPreview() {
    PmcWhAndroidTheme {
        XuatKhoScreenContent(
            issuableItems = sampleIssuableItems,
            isLoadingList = false,
            selected = null,
            qtyText = "",
            qtyValid = false,
            balance = 0.0,
            chosenRecipient = null,
            isSubmitting = false,
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onScan = {}, onSelectMaterial = {}, onQtyChange = {},
            onOpenRecipientPicker = {}, onSubmit = {}, onChooseAnother = {},
        )
    }
}

@Preview(showBackground = true, name = "Đã chọn liệu, nhập số lượng")
@Composable
private fun XuatKhoScreenDetailPreview() {
    PmcWhAndroidTheme {
        XuatKhoScreenContent(
            issuableItems = sampleIssuableItems,
            isLoadingList = false,
            selected = sampleIssuableItems[0],
            qtyText = "5",
            qtyValid = true,
            balance = 30.0,
            chosenRecipient = RecipientDto(recipientId = 1, name = "Xưởng May 1"),
            isSubmitting = false,
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {}, onScan = {}, onSelectMaterial = {}, onQtyChange = {},
            onOpenRecipientPicker = {}, onSubmit = {}, onChooseAnother = {},
        )
    }
}
