package com.samho.pmcwhandroid.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material.icons.filled.Inventory2
import androidx.compose.material.icons.filled.History
import androidx.compose.material.icons.filled.MoveToInbox
import androidx.compose.material.icons.filled.Search
import androidx.compose.material.icons.filled.Warehouse
import androidx.compose.material.icons.filled.Warning
import androidx.compose.material3.Card
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Scaffold
import androidx.compose.material3.SnackbarHost
import androidx.compose.material3.SnackbarHostState
import androidx.compose.material3.Text
import androidx.compose.material3.TopAppBar
import androidx.compose.material3.TopAppBarDefaults
import androidx.compose.runtime.Composable
import androidx.compose.runtime.remember
import androidx.compose.runtime.rememberCoroutineScope
import androidx.compose.ui.Modifier
import androidx.compose.ui.graphics.vector.ImageVector
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.data.UserSession
import kotlinx.coroutines.launch

private data class HomeMenuItem(
    val id: String,
    val title: String,
    val subtitle: String,
    val icon: ImageVector,
    val implemented: Boolean = false,
)

// Khung menu cho các phase (2-7) — item implemented=true điều hướng qua onNavigate, còn lại hiện
// "sắp có" tại chỗ. Đổi implemented=true khi phase đó xong, không cần đổi lại HomeScreen mỗi lần.
private val menuItems = listOf(
    HomeMenuItem("nhap_kho", "Nhập kho", "Liệu đang chờ (Staging) → chọn kệ → xác nhận", Icons.Filled.MoveToInbox, implemented = true),
    HomeMenuItem("xuat_kho", "Xuất kho", "Chọn liệu, số lượng, người nhận", Icons.Filled.Inventory2, implemented = true),
    HomeMenuItem("huy_lieu", "Hủy liệu", "Liệu xuất quá hạn cần đóng sổ", Icons.Filled.Warning, implemented = true),
    HomeMenuItem("tim_kiem", "Tìm kiếm", "Quét/nhập barcode ra vị trí + chi tiết", Icons.Filled.Search, implemented = true),
    HomeMenuItem("danh_sach_ke", "Danh sách kệ", "Chọn kệ + tầng xem tồn kho cụ thể", Icons.Filled.Warehouse, implemented = true),
    HomeMenuItem("log_hom_nay", "Log hôm nay", "Toàn bộ hoạt động nhập/xuất/hủy trong ngày", Icons.Filled.History, implemented = true),
)

@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun HomeScreen(session: UserSession, modifier: Modifier = Modifier, onLogout: () -> Unit, onNavigate: (String) -> Unit) {
    val snackbarHostState = remember { SnackbarHostState() }
    val scope = rememberCoroutineScope()

    Scaffold(
        modifier = modifier,
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = {
                    Column {
                        Text("PMC WH", style = MaterialTheme.typography.titleMedium)
                        Text(
                            "${session.fullName ?: session.username} · ${session.role}",
                            style = MaterialTheme.typography.bodySmall,
                        )
                    }
                },
                actions = {
                    IconButton(onClick = onLogout) {
                        Icon(Icons.AutoMirrored.Filled.Logout, contentDescription = "Đăng xuất")
                    }
                },
                colors = TopAppBarDefaults.topAppBarColors(),
            )
        },
    ) { innerPadding ->
        LazyColumn(
            modifier = Modifier
                .fillMaxSize()
                .padding(innerPadding)
                .padding(16.dp),
            verticalArrangement = Arrangement.spacedBy(10.dp),
        ) {
            items(menuItems) { item ->
                Card(
                    modifier = Modifier.fillMaxWidth(),
                    onClick = {
                        if (item.implemented) {
                            onNavigate(item.id)
                        } else {
                            scope.launch { snackbarHostState.showSnackbar("${item.title}: sắp có, đang phát triển") }
                        }
                    },
                ) {
                    ListItem(
                        headlineContent = { Text(item.title) },
                        supportingContent = { Text(item.subtitle) },
                        leadingContent = { Icon(item.icon, contentDescription = null) },
                    )
                }
            }
        }
    }
}
