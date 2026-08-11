package com.samho.pmcwhandroid.ui

import androidx.activity.compose.BackHandler
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
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Badge
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.HorizontalDivider
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
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.network.LocationMaterialDto
import com.samho.pmcwhandroid.network.WarehouseTierDto
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.launch

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun DanhSachKeScreen(onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    var tiers by remember { mutableStateOf<List<WarehouseTierDto>>(emptyList()) }
    var isLoading by remember { mutableStateOf(true) }
    var query by remember { mutableStateOf("") }
    var selectedTier by remember { mutableStateOf<WarehouseTierDto?>(null) }
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    LaunchedEffect(Unit) {
        try {
            tiers = ApiClient.warehouseApi.layout()
        } catch (e: Exception) {
            snackbarHostState.showSnackbar("Không tải được danh sách kệ: ${e.message}")
        } finally {
            isLoading = false
        }
    }

    val filtered = remember(tiers, query) {
        if (query.isBlank()) tiers else tiers.filter { it.code.contains(query, ignoreCase = true) }
    }

    DanhSachKeScreenContent(
        filtered = filtered,
        isLoading = isLoading,
        query = query,
        onQueryChange = { query = it },
        onBack = onBack,
        onTierClick = { tier -> selectedTier = tier },
        snackbarHostState = snackbarHostState,
    )

    selectedTier?.let { tier ->
        TierDetailDialog(tier = tier, onDismiss = { selectedTier = null }, snackbarHostState = snackbarHostState, scope = scope)
    }
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun DanhSachKeScreenContent(
    filtered: List<WarehouseTierDto>,
    isLoading: Boolean,
    query: String,
    onQueryChange: (String) -> Unit,
    onBack: () -> Unit,
    onTierClick: (WarehouseTierDto) -> Unit,
    snackbarHostState: SnackbarHostState,
) {
    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Danh sách kệ") },
                navigationIcon = {
                    IconButton(onClick = onBack) {
                        Icon(Icons.AutoMirrored.Filled.ArrowBack, contentDescription = "Quay lại")
                    }
                },
            )
        },
    ) { padding ->
        Column(modifier = Modifier.fillMaxSize().padding(padding).padding(horizontal = 16.dp)) {
            Spacer(Modifier.height(12.dp))
            OutlinedTextField(
                value = query,
                onValueChange = onQueryChange,
                label = { Text("Tìm theo mã kệ (vd: 40 hoặc 40.1)") },
                singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
            Spacer(Modifier.height(12.dp))

            Box(modifier = Modifier.fillMaxSize()) {
                when {
                    isLoading -> CircularProgressIndicator(modifier = Modifier.align(Alignment.TopCenter).padding(top = 24.dp))
                    filtered.isEmpty() -> Text(
                        "Không tìm thấy kệ nào khớp.",
                        modifier = Modifier.align(Alignment.TopCenter).padding(24.dp),
                    )
                    else -> LazyColumn(modifier = Modifier.fillMaxSize()) {
                        items(filtered, key = { it.locationId }) { tier ->
                            Card(
                                modifier = Modifier.fillMaxWidth().padding(vertical = 4.dp),
                                onClick = { onTierClick(tier) },
                            ) {
                                ListItem(
                                    headlineContent = { Text("Kệ ${tier.code}") },
                                    supportingContent = { Text("Rack ${tier.rackNo} · Tầng ${tier.levelNo}") },
                                    trailingContent = {
                                        Badge(
                                            containerColor = if (tier.qrCount > 0) MaterialTheme.colorScheme.primary else MaterialTheme.colorScheme.surfaceVariant,
                                            contentColor = if (tier.qrCount > 0) MaterialTheme.colorScheme.onPrimary else MaterialTheme.colorScheme.onSurfaceVariant,
                                        ) { Text("${tier.qrCount} mã") }
                                    },
                                )
                            }
                        }
                    }
                }
            }
        }
    }
}

@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun TierDetailDialog(
    tier: WarehouseTierDto,
    onDismiss: () -> Unit,
    snackbarHostState: SnackbarHostState,
    scope: kotlinx.coroutines.CoroutineScope,
) {
    var items by remember { mutableStateOf<List<LocationMaterialDto>>(emptyList()) }
    var page by remember { mutableStateOf(1) }
    var totalPages by remember { mutableStateOf(1) }
    var isLoading by remember { mutableStateOf(true) }

    fun load(p: Int) {
        isLoading = true
        scope.launch {
            try {
                val result = ApiClient.warehouseApi.locationMaterials(tier.locationId, page = p)
                items = result.items
                page = result.page
                totalPages = if (result.totalPages > 0) result.totalPages else 1
            } catch (e: Exception) {
                snackbarHostState.showSnackbar("Không tải được chi tiết kệ: ${e.message}")
            } finally {
                isLoading = false
            }
        }
    }

    LaunchedEffect(tier.locationId) { load(1) }

    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = onDismiss) { Text("Đóng") } },
        title = { Text("Kệ ${tier.code} — ${tier.qrCount} mã") },
        text = {
            Box(modifier = Modifier.fillMaxWidth().height(360.dp)) {
                when {
                    isLoading -> CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                    items.isEmpty() -> Text("Kệ này đang trống.", modifier = Modifier.align(Alignment.Center))
                    else -> Column {
                        LazyColumn(modifier = Modifier.weight(1f, fill = false).fillMaxWidth()) {
                            items(items, key = { it.materialId }) { m ->
                                ListItem(
                                    headlineContent = { Text(m.barcode) },
                                    supportingContent = {
                                        Text(listOfNotNull(m.dev, m.model, m.sizeSpec).joinToString(" / "))
                                    },
                                    trailingContent = { Text("${m.balance ?: 0} ${m.unit ?: ""}") },
                                )
                                HorizontalDivider()
                            }
                        }
                        if (totalPages > 1) {
                            Row(
                                modifier = Modifier.fillMaxWidth().padding(top = 8.dp),
                                horizontalArrangement = androidx.compose.foundation.layout.Arrangement.SpaceBetween,
                            ) {
                                TextButton(enabled = page > 1 && !isLoading, onClick = { load(page - 1) }) { Text("‹ Trước") }
                                Text("Trang $page / $totalPages", modifier = Modifier.align(Alignment.CenterVertically))
                                TextButton(enabled = page < totalPages && !isLoading, onClick = { load(page + 1) }) { Text("Sau ›") }
                            }
                        }
                    }
                }
            }
        },
    )
}

private val sampleTiers = listOf(
    WarehouseTierDto(locationId = 1, rackNo = 1, levelNo = 1, code = "1.1", qrCount = 5),
    WarehouseTierDto(locationId = 2, rackNo = 1, levelNo = 2, code = "1.2", qrCount = 0),
    WarehouseTierDto(locationId = 3, rackNo = 40, levelNo = 1, code = "40.1", qrCount = 12),
)

@Preview(showBackground = true, name = "Danh sách kệ")
@Composable
private fun DanhSachKeScreenPreview() {
    PmcWhAndroidTheme {
        DanhSachKeScreenContent(
            filtered = sampleTiers,
            isLoading = false,
            query = "",
            onQueryChange = {},
            onBack = {},
            onTierClick = {},
            snackbarHostState = remember { SnackbarHostState() },
        )
    }
}
