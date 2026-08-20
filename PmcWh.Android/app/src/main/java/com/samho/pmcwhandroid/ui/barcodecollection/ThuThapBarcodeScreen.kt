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
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Add
import androidx.compose.material.icons.filled.Delete
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FloatingActionButton
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
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.BarcodeListDto
import com.samho.pmcwhandroid.network.CreateBarcodeListRequest
import com.samho.pmcwhandroid.network.errorMessageOrDefault
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.launch

/**
 * "Thu thập Barcode" (PMC feedback): quét trên Android, xem/xoá/xuất Excel trên Web — cùng 1 API
 * (BarcodeCollectionApi), không lưu gì local trên máy. Màn này = danh sách các list đã tạo + tạo
 * list mới; bấm vào 1 list chuyển qua [BarcodeListDetailScreen] để quét.
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun ThuThapBarcodeScreen(session: UserSession, onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    var lists by remember { mutableStateOf<List<BarcodeListDto>>(emptyList()) }
    var isLoading by remember { mutableStateOf(false) }
    var showCreateDialog by remember { mutableStateOf(false) }
    var pendingDeleteList by remember { mutableStateOf<BarcodeListDto?>(null) }
    var selectedList by remember { mutableStateOf<BarcodeListDto?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    suspend fun loadLists() {
        isLoading = true
        try {
            lists = ApiClient.barcodeCollectionApi.getLists()
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Không tải được danh sách: ${e.message}")
        } finally {
            isLoading = false
        }
    }

    LaunchedEffect(Unit) { loadLists() }

    val current = selectedList
    if (current != null) {
        BarcodeListDetailScreen(
            list = current,
            onBack = {
                selectedList = null
                scope.launch { loadLists() }
            },
        )
        return
    }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Thu thập Barcode") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Quay lại")
                    }
                },
            )
        },
        floatingActionButton = {
            FloatingActionButton(onClick = { showCreateDialog = true }) {
                Icon(Icons.Filled.Add, contentDescription = "Tạo list mới")
            }
        },
    ) { padding ->
        Box(modifier = Modifier.fillMaxSize().padding(padding)) {
            if (isLoading && lists.isEmpty()) {
                CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
            } else if (lists.isEmpty()) {
                Text(
                    "Chưa có list nào — bấm nút + để tạo list mới.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                )
            } else {
                LazyColumn(modifier = Modifier.fillMaxSize().padding(12.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
                    items(lists, key = { it.listId }) { list ->
                        Card(
                            modifier = Modifier.fillMaxWidth(),
                            colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                            onClick = { selectedList = list },
                        ) {
                            ListItem(
                                headlineContent = { Text(list.name, fontWeight = androidx.compose.ui.text.font.FontWeight.Medium) },
                                supportingContent = { Text("${list.itemCount} mã · ${list.totalScans} lượt quét") },
                                trailingContent = {
                                    IconButton(onClick = { pendingDeleteList = list }) {
                                        Icon(Icons.Filled.Delete, contentDescription = "Xoá list")
                                    }
                                },
                            )
                        }
                    }
                }
            }
        }
    }

    if (showCreateDialog) {
        CreateListDialog(
            onDismiss = { showCreateDialog = false },
            onCreate = { name ->
                scope.launch {
                    try {
                        val resp = ApiClient.barcodeCollectionApi.createList(CreateBarcodeListRequest(name = name, userId = session.userId))
                        val created = resp.body()
                        if (resp.isSuccessful && created != null) {
                            showCreateDialog = false
                            loadLists()
                            selectedList = created
                        } else {
                            snackbarHostState.showSnackbar(resp.errorMessageOrDefault("Không tạo được list."))
                        }
                    } catch (e: Exception) {
                        snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
                    }
                }
            },
        )
    }

    pendingDeleteList?.let { list ->
        AlertDialog(
            onDismissRequest = { pendingDeleteList = null },
            title = { Text("Xoá list?") },
            text = { Text("Xoá list \"${list.name}\" — mất hết ${list.itemCount} mã đã quét, không thể hoàn tác.") },
            confirmButton = {
                Button(onClick = {
                    pendingDeleteList = null
                    scope.launch {
                        try {
                            ApiClient.barcodeCollectionApi.deleteList(list.listId)
                            loadLists()
                        } catch (e: Exception) {
                            snackbarHostState.showSnackbar("Lỗi mạng: ${e.message}")
                        }
                    }
                }) { Text("Xoá") }
            },
            dismissButton = {
                TextButton(onClick = { pendingDeleteList = null }) { Text("Thôi") }
            },
        )
    }
}

@Composable
private fun CreateListDialog(onDismiss: () -> Unit, onCreate: (String) -> Unit) {
    var name by remember { mutableStateOf("") }

    AlertDialog(
        onDismissRequest = onDismiss,
        title = { Text("Tạo Barcode List") },
        text = {
            Column {
                Text("Đặt tên cho list, VD: Liệu test 20/08", style = MaterialTheme.typography.bodySmall)
                Spacer(Modifier.height(8.dp))
                OutlinedTextField(
                    value = name,
                    onValueChange = { name = it },
                    label = { Text("Tên list") },
                    singleLine = true,
                    modifier = Modifier.fillMaxWidth(),
                )
            }
        },
        confirmButton = {
            Button(enabled = name.isNotBlank(), onClick = { onCreate(name.trim()) }) { Text("Tạo") }
        },
        dismissButton = {
            TextButton(onClick = onDismiss) { Text("Huỷ") }
        },
    )
}

@Preview(showBackground = true)
@Composable
private fun ThuThapBarcodeScreenPreview() {
    PmcWhAndroidTheme {
        ThuThapBarcodeScreen(
            session = UserSession(userId = 1, username = "demo", fullName = "Nguyễn Văn A", role = "Admin", token = null),
            onBack = {},
        )
    }
}
