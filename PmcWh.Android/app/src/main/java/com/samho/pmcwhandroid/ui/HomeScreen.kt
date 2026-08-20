package com.samho.pmcwhandroid.ui

import androidx.compose.foundation.BorderStroke
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.Logout
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.ListItem
import androidx.compose.material3.ListItemDefaults
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
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.tooling.preview.Preview
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.launch

private data class HomeMenuItem(
    val id: String,
    val title: String,
    val emoji: String,
    val implemented: Boolean = false,
)

// Khung menu cho các phase (2-7) — item implemented=true điều hướng qua onNavigate, còn lại hiện
// "sắp có" tại chỗ. Đổi implemented=true khi phase đó xong, không cần đổi lại HomeScreen mỗi lần.
private val menuItems = listOf(
    HomeMenuItem("nhap_kho", "Nhập kho", "📥", implemented = true),
    HomeMenuItem("xuat_kho", "Xuất kho", "📤", implemented = true),
    HomeMenuItem("nhan_lai", "Nhận lại", "↩️", implemented = true),
    HomeMenuItem("huy_lieu", "Hủy liệu", "🗑️", implemented = true),
    HomeMenuItem("tim_kiem", "Tìm kiếm", "🔍", implemented = true),
    HomeMenuItem("danh_sach_ke", "Danh sách kệ", "🏬", implemented = true),
    HomeMenuItem("log_hom_nay", "Log hôm nay", "🕒", implemented = true),
    HomeMenuItem("thu_thap_barcode", "Thu thập Barcode", "📊", implemented = true),
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
                        Text("PMC WAREHOUSE", style = MaterialTheme.typography.titleMedium)
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
                    colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.surface),
                    border = BorderStroke(1.dp, MaterialTheme.colorScheme.outlineVariant),
                    onClick = {
                        if (item.implemented) {
                            onNavigate(item.id)
                        } else {
                            scope.launch { snackbarHostState.showSnackbar("${item.title}: sắp có, đang phát triển") }
                        }
                    },
                ) {
                    ListItem(
                        headlineContent = { Text(item.title, fontWeight = FontWeight.Medium) },
                        leadingContent = { Text(item.emoji, fontSize = 26.sp) },
                        colors = ListItemDefaults.colors(containerColor = Color.Transparent),
                    )
                }
            }
        }
    }
}

@Preview(showBackground = true)
@Composable
private fun HomeScreenPreview() {
    PmcWhAndroidTheme {
        HomeScreen(
            session = UserSession(userId = 1, username = "demo", fullName = "Nguyễn Văn A", role = "Admin", token = null),
            onLogout = {},
            onNavigate = {},
        )
    }
}
