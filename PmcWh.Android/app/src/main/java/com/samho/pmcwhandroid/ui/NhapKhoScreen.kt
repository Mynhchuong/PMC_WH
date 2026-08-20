package com.samho.pmcwhandroid.ui

import androidx.activity.compose.BackHandler
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.automirrored.filled.ArrowBack
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.ExperimentalMaterial3Api
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
import com.samho.pmcwhandroid.network.StorageLocationDto
import com.samho.pmcwhandroid.ui.assign.AssignMaterialsScreen
import com.samho.pmcwhandroid.ui.components.LocationPickerDialog
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme
import kotlinx.coroutines.launch

/**
 * Chọn 1 kệ trước, rồi chuyển qua [AssignMaterialsScreen] (quét theo lô, dùng chung với Danh Sách
 * Kệ) để gán liệu Staging vào kệ đó. Cố ý KHÔNG hiện danh sách liệu nào để bấm chọn thủ công —
 * chỉ quét mới thêm được vào danh sách chờ, tránh công nhân bấm nhầm liệu (thao tác sai).
 */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
fun NhapKhoScreen(session: UserSession, onBack: () -> Unit) {
    BackHandler(onBack = onBack)
    var currentLocation by remember { mutableStateOf<StorageLocationDto?>(null) }
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

    val location = currentLocation
    if (location != null) {
        AssignMaterialsScreen(
            session = session,
            location = location,
            onClose = { currentLocation = null },
            onAllSavedAndClosed = { currentLocation = null },
        )
    } else {
        NhapKhoScreenContent(
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
            onSelect = { loc -> currentLocation = loc; showLocationPicker = false },
        )
    }
}

/** Phần giao diện thuần (không gọi API) — tách riêng để @Preview render được với dữ liệu mẫu. */
@OptIn(ExperimentalMaterial3Api::class)
@Composable
private fun NhapKhoScreenContent(
    snackbarHostState: SnackbarHostState,
    onBack: () -> Unit,
    onChooseLocation: () -> Unit,
) {
    Scaffold(
        snackbarHost = { SnackbarHost(snackbarHostState) },
        topBar = {
            TopAppBar(
                title = { Text("Nhập kho") },
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
                    Text("Chọn kệ để bắt đầu quét gán liệu", style = MaterialTheme.typography.titleMedium)
                    TextButton(onClick = onChooseLocation) { Text("Chọn kệ") }
                }
            }

            Box(modifier = Modifier.fillMaxSize()) {
                Text(
                    "Chọn kệ rồi quét mã để gán liệu vào kệ đó.",
                    color = MaterialTheme.colorScheme.onSurfaceVariant,
                    modifier = Modifier.align(Alignment.Center).padding(24.dp),
                )
            }
        }
    }
}

@Preview(showBackground = true, name = "Chưa chọn kệ")
@Composable
private fun NhapKhoScreenPreview() {
    PmcWhAndroidTheme {
        NhapKhoScreenContent(
            snackbarHostState = remember { SnackbarHostState() },
            onBack = {},
            onChooseLocation = {},
        )
    }
}
