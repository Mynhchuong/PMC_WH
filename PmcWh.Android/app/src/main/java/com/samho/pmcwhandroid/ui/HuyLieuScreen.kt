package com.samho.pmcwhandroid.ui

import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.QrCodeScanner
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
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
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.DisposeRequest
import com.samho.pmcwhandroid.network.OverdueIssuedItem
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.scan.rememberBarcodeScanner
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HuyLieuScreen(session: UserSession, onBack: () -> Unit) {
    var overdueItems by remember { mutableStateOf<List<OverdueIssuedItem>>(emptyList()) }
    var isLoadingList by remember { mutableStateOf(true) }
    var selected by remember { mutableStateOf<OverdueIssuedItem?>(null) }
    var showConfirm by remember { mutableStateOf(false) }
    var isSubmitting by remember { mutableStateOf(false) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    suspend fun loadOverdue() {
        isLoadingList = true
        try {
            overdueItems = ApiClient.materialsApi.overdueIssued()
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Không tải được danh sách: ${e.message}")
        } finally {
            isLoadingList = false
        }
    }

    LaunchedEffect(Unit) { loadOverdue() }

    val scanLauncher = rememberBarcodeScanner(
        onResult = { code ->
            val match = overdueItems.firstOrNull { it.barcode.equals(code, ignoreCase = true) }
            if (match != null) {
                selected = match
            } else {
                scope.launch {
                    snackbarHostState.showSnackbar("Mã '$code' không nằm trong danh sách xuất quá hạn.")
                }
            }
        },
        onError = { msg -> scope.launch { snackbarHostState.showSnackbar(msg) } },
    )

    suspend fun submitDispose() {
        val material = selected ?: return
        isSubmitting = true
        try {
            val resp = ApiClient.materialsApi.dispose(material.materialId, DisposeRequest(userId = session.userId))
            if (resp.isSuccessful) {
                snackbarHostState.showSnackbar("Đã hủy ${material.barcode}")
                selected = null
                loadOverdue()
            } else {
                snackbarHostState.showSnackbar(resp.errorMessageOrDefault("Hủy liệu thất bại."))
            }
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
        } finally {
            isSubmitting = false
            showConfirm = false
        }
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Hủy liệu quá hạn") },
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
        val currentSelected = selected
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            if (currentSelected == null) {
                when {
                    isLoadingList -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                    overdueItems.isEmpty() -> Text(
                        "Không có liệu nào xuất quá hạn.",
                        modifier = Modifier.align(Alignment.Center).padding(24.dp),
                    )
                    else -> LazyColumn(modifier = Modifier.fillMaxSize()) {
                        items(overdueItems, key = { it.materialId }) { item ->
                            Card(
                                modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp),
                                onClick = { selected = item },
                            ) {
                                ListItem(
                                    headlineContent = { Text(item.barcode) },
                                    supportingContent = {
                                        Text(
                                            listOfNotNull(item.dev, item.matlDescription, item.recipientName)
                                                .joinToString(" / "),
                                        )
                                    },
                                    trailingContent = {
                                        Text(
                                            "${item.daysOut} ngày",
                                            color = MaterialTheme.colorScheme.error,
                                            style = MaterialTheme.typography.labelLarge,
                                        )
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
                            Text(currentSelected.barcode, style = MaterialTheme.typography.titleLarge)
                            Text(
                                listOfNotNull(currentSelected.dev, currentSelected.matlDescription).joinToString(" / "),
                                style = MaterialTheme.typography.bodyMedium,
                            )
                            Spacer(Modifier.height(8.dp))
                            Text("Người nhận: ${currentSelected.recipientName ?: "—"}")
                            Text("Còn lại: ${currentSelected.balance ?: 0} ${currentSelected.unit ?: ""}")
                            Text(
                                "Đã xuất quá hạn: ${currentSelected.daysOut} ngày",
                                color = MaterialTheme.colorScheme.error,
                                style = MaterialTheme.typography.titleMedium,
                            )
                        }
                    }

                    Spacer(Modifier.height(24.dp))

                    Button(
                        modifier = Modifier.fillMaxWidth(),
                        colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error),
                        enabled = !isSubmitting,
                        onClick = { showConfirm = true },
                    ) {
                        Text("Hủy liệu này")
                    }

                    Spacer(Modifier.height(8.dp))

                    TextButton(
                        modifier = Modifier.fillMaxWidth(),
                        onClick = { selected = null },
                    ) {
                        Text("Chọn liệu khác")
                    }
                }
            }
        }
    }

    if (showConfirm && selected != null) {
        AlertDialog(
            onDismissRequest = { if (!isSubmitting) showConfirm = false },
            title = { Text("Xác nhận hủy liệu") },
            text = {
                Text(
                    "Hủy '${selected?.barcode}' — thao tác này KHÔNG thể hoàn tác. " +
                        "Số lượng còn lại (${selected?.balance ?: 0} ${selected?.unit ?: ""}) sẽ về 0.",
                )
            },
            confirmButton = {
                Button(
                    colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.error),
                    enabled = !isSubmitting,
                    onClick = { scope.launch { submitDispose() } },
                ) {
                    Text(if (isSubmitting) "Đang xử lý..." else "Hủy liệu")
                }
            },
            dismissButton = {
                TextButton(enabled = !isSubmitting, onClick = { showConfirm = false }) { Text("Thôi") }
            },
        )
    }
}
