package com.samho.pmcwhandroid.ui.components

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.ListItem
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.network.StorageLocationDto

/**
 * Chọn kệ theo 2 bước: chọn số kệ (rack) trước, rồi chọn tầng (level) trong kệ đó. Dùng chung cho
 * Nhập kho (chọn kệ đích trước khi quét) và Nhận lại (chọn kệ mới cho liệu đã rời kệ hẳn).
 */
@Composable
fun LocationPickerDialog(
    locations: List<StorageLocationDto>,
    isLoading: Boolean,
    onDismiss: () -> Unit,
    onSelect: (StorageLocationDto) -> Unit,
) {
    var query by remember { mutableStateOf("") }
    var selectedRack by remember { mutableStateOf<Int?>(null) }

    val racks = remember(locations) { locations.map { it.rackNo }.distinct().sorted() }
    val filteredRacks = remember(racks, query) {
        if (query.isBlank()) racks else racks.filter { it.toString().contains(query) }
    }
    val tiersInRack = remember(locations, selectedRack) {
        val rack = selectedRack
        if (rack == null) emptyList() else locations.filter { it.rackNo == rack }.sortedBy { it.levelNo }
    }

    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = onDismiss) { Text("Đóng") } },
        dismissButton = if (selectedRack != null) {
            { TextButton(onClick = { selectedRack = null }) { Text("‹ Chọn kệ khác") } }
        } else {
            null
        },
        title = { Text(if (selectedRack == null) "Chọn kệ" else "Chọn tầng — Kệ ${selectedRack}") },
        text = {
            Column {
                if (selectedRack == null) {
                    OutlinedTextField(
                        value = query,
                        onValueChange = { query = it },
                        label = { Text("Tìm theo số kệ (vd: 40)") },
                        singleLine = true,
                        modifier = Modifier.fillMaxWidth(),
                    )
                    Spacer(Modifier.height(8.dp))
                }
                Box(modifier = Modifier.fillMaxWidth().height(320.dp)) {
                    if (isLoading) {
                        CircularProgressIndicator(modifier = Modifier.align(Alignment.Center))
                    } else if (selectedRack == null) {
                        LazyColumn {
                            items(filteredRacks, key = { it }) { rack ->
                                ListItem(
                                    headlineContent = { Text("Kệ $rack") },
                                    supportingContent = { Text("${locations.count { it.rackNo == rack }} tầng") },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .clickable { selectedRack = rack },
                                )
                                HorizontalDivider()
                            }
                        }
                    } else {
                        LazyColumn {
                            items(tiersInRack, key = { it.locationId }) { loc ->
                                ListItem(
                                    headlineContent = { Text("Tầng ${loc.levelNo}") },
                                    supportingContent = { Text(loc.code) },
                                    modifier = Modifier
                                        .fillMaxWidth()
                                        .clickable { onSelect(loc) },
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
