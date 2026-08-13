package com.samho.pmcwhandroid.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.AlertDialog
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import com.samho.pmcwhandroid.network.MaterialListItem

/**
 * Popup xem đầy đủ thông tin 1 liệu — dùng khi công nhân nghi ngờ quét nhầm, cần xem lại chi tiết
 * liệu vừa quét (vd trong danh sách chờ ở Nhập/Xuất kho) mà không phải qua màn Tìm kiếm riêng.
 */
@Composable
fun MaterialDetailDialog(item: MaterialListItem, onDismiss: () -> Unit) {
    AlertDialog(
        onDismissRequest = onDismiss,
        confirmButton = { TextButton(onClick = onDismiss) { Text("Đóng") } },
        title = {
            Row(horizontalArrangement = Arrangement.SpaceBetween, modifier = Modifier.fillMaxWidth()) {
                Text(item.barcode, style = MaterialTheme.typography.titleLarge, fontWeight = FontWeight.Bold)
                StatusBadge(item.status, item.isOverdue)
            }
        },
        text = { MaterialDetailBody(item) },
    )
}

@Composable
fun MaterialDetailBody(item: MaterialListItem) {
    Column {
        InfoRow("Vị trí", item.locationCode ?: "— (không trong kho)")
        InfoRow("Dev", item.dev ?: "—")
        InfoRow("PO", item.poNo ?: "—")
        InfoRow("Nhà cung cấp", item.supplier ?: "—")
        InfoRow("Model", item.model ?: "—")
        InfoRow("Season", item.season ?: "—")
        InfoRow("Stage", item.stage ?: "—")
        InfoRow("Colorway", item.colorway ?: "—")
        InfoRow("Tên liệu", item.matlDescription ?: "—")
        InfoRow("Màu", item.colorCode ?: "—")
        InfoRow("Size", item.sizeSpec ?: "—")
        InfoRow("SL nhập kho", "${item.arrivalQty ?: 0} ${item.unit ?: ""}")
        InfoRow("Tồn hiện tại", "${item.balance ?: 0} ${item.unit ?: ""}")
    }
}

@Composable
fun InfoRow(label: String, value: String) {
    Row(modifier = Modifier.fillMaxWidth().padding(vertical = 3.dp)) {
        Text(label, style = MaterialTheme.typography.bodyMedium, color = MaterialTheme.colorScheme.onSurfaceVariant, modifier = Modifier.weight(1f))
        Text(value, style = MaterialTheme.typography.bodyMedium, fontWeight = FontWeight.Medium, modifier = Modifier.weight(1.4f))
    }
}

@Composable
fun StatusBadge(status: String, isOverdue: Boolean) {
    val (bg, fg, label) = when {
        isOverdue -> Triple(MaterialTheme.colorScheme.errorContainer, MaterialTheme.colorScheme.onErrorContainer, "Quá hạn")
        status == "InStock" -> Triple(MaterialTheme.colorScheme.primaryContainer, MaterialTheme.colorScheme.onPrimaryContainer, "Trong kho")
        status == "PartiallyIssued" -> Triple(MaterialTheme.colorScheme.tertiaryContainer, MaterialTheme.colorScheme.onTertiaryContainer, "Xuất 1 phần")
        status == "IssuedOut" -> Triple(MaterialTheme.colorScheme.surfaceVariant, MaterialTheme.colorScheme.onSurfaceVariant, "Đã xuất hết")
        status == "Staging" -> Triple(MaterialTheme.colorScheme.secondaryContainer, MaterialTheme.colorScheme.onSecondaryContainer, "Chờ nhập")
        status == "Disposed" -> Triple(MaterialTheme.colorScheme.errorContainer, MaterialTheme.colorScheme.onErrorContainer, "Đã hủy")
        else -> Triple(MaterialTheme.colorScheme.surfaceVariant, MaterialTheme.colorScheme.onSurfaceVariant, status)
    }
    Surface(color = bg, contentColor = fg, shape = MaterialTheme.shapes.small) {
        Text(label, style = MaterialTheme.typography.labelMedium, modifier = Modifier.padding(horizontal = 10.dp, vertical = 4.dp))
    }
}
