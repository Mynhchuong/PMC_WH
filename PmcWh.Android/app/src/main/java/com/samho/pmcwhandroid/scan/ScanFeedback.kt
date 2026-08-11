package com.samho.pmcwhandroid.scan

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.CheckCircle
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Error
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

/**
 * Kết quả 1 lần quét — hiện ngay cho người quét biết quét được hay lỗi (không dựa vào Snackbar,
 * vì khi camera đang mở dạng Dialog toàn màn hình thì Snackbar của màn chính bị che mất, không
 * thấy được). Banner tự thay nội dung khi có mã mới, hoặc bấm X để tắt.
 */
sealed class ScanFeedback {
    abstract val barcode: String
    abstract val message: String

    data class Success(override val barcode: String, override val message: String) : ScanFeedback()
    data class Failure(override val barcode: String, override val message: String) : ScanFeedback()
}

@Composable
fun ScanFeedbackBanner(
    feedback: ScanFeedback?,
    onDismiss: () -> Unit,
    modifier: Modifier = Modifier,
) {
    if (feedback == null) return
    val isSuccess = feedback is ScanFeedback.Success
    Surface(
        color = if (isSuccess) MaterialTheme.colorScheme.primaryContainer else MaterialTheme.colorScheme.errorContainer,
        contentColor = if (isSuccess) MaterialTheme.colorScheme.onPrimaryContainer else MaterialTheme.colorScheme.onErrorContainer,
        shape = MaterialTheme.shapes.medium,
        modifier = modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp),
    ) {
        Row(
            modifier = Modifier.fillMaxWidth().padding(start = 12.dp, end = 4.dp, top = 8.dp, bottom = 8.dp),
            horizontalArrangement = Arrangement.SpaceBetween,
            verticalAlignment = Alignment.CenterVertically,
        ) {
            Row(verticalAlignment = Alignment.CenterVertically, modifier = Modifier.weight(1f)) {
                Icon(if (isSuccess) Icons.Filled.CheckCircle else Icons.Filled.Error, contentDescription = null)
                Spacer(Modifier.width(8.dp))
                Column {
                    Text(feedback.barcode, style = MaterialTheme.typography.titleSmall)
                    Text(feedback.message, style = MaterialTheme.typography.bodySmall)
                }
            }
            IconButton(onClick = onDismiss) {
                Icon(Icons.Filled.Close, contentDescription = "Đóng")
            }
        }
    }
}
