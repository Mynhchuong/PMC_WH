package com.samho.pmcwhandroid.ui

import androidx.compose.foundation.horizontalScroll
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.rememberScrollState
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material.icons.filled.Refresh
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.FilterChip
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Surface
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
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.TodayLogItem
import kotlinx.coroutines.launch

private data class TypeFilter(val label: String, val value: String?)

private val typeFilters = listOf(
    TypeFilter("Tất cả", null),
    TypeFilter("Nhập kho", "Inbound"),
    TypeFilter("Xuất kho", "IssueToWorkshop"),
    TypeFilter("Nhận lại", "Return"),
    TypeFilter("Hủy", "Dispose"),
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun LogHomNayScreen(onBack: () -> Unit) {
    var items by remember { mutableStateOf<List<TodayLogItem>>(emptyList()) }
    var page by remember { mutableStateOf(1) }
    var totalPages by remember { mutableStateOf(1) }
    var totalCount by remember { mutableStateOf(0) }
    var isLoading by remember { mutableStateOf(true) }
    var selectedFilter by remember { mutableStateOf<TypeFilter>(typeFilters[0]) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    fun load(p: Int) {
        isLoading = true
        scope.launch {
            try {
                val result = ApiClient.warehouseApi.todayLog(movementType = selectedFilter.value, page = p)
                items = result.items
                page = result.page
                totalPages = if (result.totalPages > 0) result.totalPages else 1
                totalCount = result.totalCount
            } catch (e: Exception) {
                snackbarHostState.showSnackbar("Không tải được log: ${e.message}")
            } finally {
                isLoading = false
            }
        }
    }

    LaunchedEffect(selectedFilter) { load(1) }

    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Log hôm nay ($totalCount)") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Quay lại")
                    }
                },
                actions = {
                    IconButton(onClick = { load(page) }) {
                        Icon(Icons.Filled.Refresh, contentDescription = "Tải lại")
                    }
                },
            )
        },
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding)) {
            Row(
                modifier = Modifier.fillMaxWidth().horizontalScroll(rememberScrollState()).padding(horizontal = 12.dp, vertical = 8.dp),
                horizontalArrangement = Arrangement.spacedBy(8.dp),
            ) {
                typeFilters.forEach { f ->
                    FilterChip(
                        selected = selectedFilter == f,
                        onClick = { selectedFilter = f },
                        label = { Text(f.label) },
                    )
                }
            }

            Box(modifier = Modifier.fillMaxSize()) {
                when {
                    isLoading -> CircularProgressIndicator(modifier = Modifier.align(Alignment.TopCenter).padding(top = 24.dp))
                    items.isEmpty() -> Text(
                        "Không có hoạt động nào.",
                        modifier = Modifier.align(Alignment.TopCenter).padding(24.dp),
                    )
                    else -> Column(modifier = Modifier.fillMaxSize()) {
                        LazyColumn(modifier = Modifier.weight(1f).fillMaxWidth()) {
                            items(items, key = { it.movementId }) { entry ->
                                LogRow(entry)
                            }
                        }
                        if (totalPages > 1) {
                            Row(
                                modifier = Modifier.fillMaxWidth().padding(12.dp),
                                horizontalArrangement = Arrangement.SpaceBetween,
                            ) {
                                TextButton(enabled = page > 1 && !isLoading, onClick = { load(page - 1) }) { Text("‹ Trước") }
                                Text("Trang $page / $totalPages", modifier = Modifier.align(Alignment.CenterVertically))
                                TextButton(enabled = page < totalPages && !isLoading, onClick = { load(page + 1) }) { Text("Sau ›") }
                            }
                        }
                    }
                }
            }
        }
    }
}

private fun typeLabel(type: String): String = when (type) {
    "Inbound" -> "Nhập kho"
    "IssueToWorkshop" -> "Xuất kho"
    "Return" -> "Nhận lại"
    "Dispose" -> "Hủy"
    else -> type
}

@Composable
private fun LogRow(entry: TodayLogItem) {
    val (bg, fg) = when (entry.movementType) {
        "Inbound" -> MaterialTheme.colorScheme.primaryContainer to MaterialTheme.colorScheme.onPrimaryContainer
        "IssueToWorkshop" -> MaterialTheme.colorScheme.tertiaryContainer to MaterialTheme.colorScheme.onTertiaryContainer
        "Return" -> MaterialTheme.colorScheme.secondaryContainer to MaterialTheme.colorScheme.onSecondaryContainer
        "Dispose" -> MaterialTheme.colorScheme.errorContainer to MaterialTheme.colorScheme.onErrorContainer
        else -> MaterialTheme.colorScheme.surfaceVariant to MaterialTheme.colorScheme.onSurfaceVariant
    }
    // Server đã trả ISO "yyyy-MM-ddTHH:mm:ss..." — cắt chuỗi lấy giờ:phút, tránh phải dùng java.time
    // (API 26+) trong khi app hỗ trợ tới minSdk 23.
    val time = entry.occurredAt.let { if (it.length >= 16) it.substring(11, 16) else it }
    val context = when (entry.movementType) {
        "IssueToWorkshop", "Return" -> entry.recipientName
        else -> entry.locationCode
    }

    Card(modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp)) {
        ListItem(
            overlineContent = {
                Surface(color = bg, contentColor = fg, shape = MaterialTheme.shapes.small) {
                    Text(
                        typeLabel(entry.movementType),
                        style = MaterialTheme.typography.labelSmall,
                        modifier = Modifier.padding(horizontal = 8.dp, vertical = 2.dp),
                    )
                }
            },
            headlineContent = { Text(entry.barcode) },
            supportingContent = {
                Text(listOfNotNull(entry.dev, entry.model, context).joinToString(" / ").ifBlank { "—" })
            },
            trailingContent = {
                Column(horizontalAlignment = Alignment.End) {
                    Text("${entry.qty} ${entry.unit ?: ""}", style = MaterialTheme.typography.bodyMedium)
                    Text(time, style = MaterialTheme.typography.labelSmall, color = MaterialTheme.colorScheme.onSurfaceVariant)
                }
            },
        )
    }
}
