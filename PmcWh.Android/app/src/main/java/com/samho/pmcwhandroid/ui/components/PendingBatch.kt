package com.samho.pmcwhandroid.ui.components

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.Close
import androidx.compose.material.icons.filled.Info
import androidx.compose.material3.Card
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.unit.dp

enum class PendingRowStatus { PENDING, SAVING, ERROR }

/**
 * Model dùng chung cho danh sách chờ lưu — mỗi màn tự có data class dòng riêng (implement
 * interface này) vì dữ liệu gắn kèm khác nhau (vd. Xuất kho cần thêm ô số lượng riêng từng dòng),
 * nhưng đều theo cùng khuôn status/key/error để [PendingBatchList] và logic Lưu-theo-lô nhất quán
 * giữa các màn.
 */
interface PendingRow {
    val key: Long
    val status: PendingRowStatus
    val error: String?
}

/**
 * Danh sách các dòng đang chờ lưu — [rowContent] là phần nội dung riêng của từng màn (barcode,
 * mô tả, ô số lượng...); nút xoá + trạng thái đang lưu/lỗi được vẽ chung ở đây.
 *
 * [onRowClick] (tuỳ chọn) thêm 1 nút "xem chi tiết" riêng cho mỗi dòng — dùng nút riêng thay vì
 * bấm cả dòng, vì [rowContent] có thể chứa ô nhập liệu tương tác (vd ô số lượng ở Xuất kho), bấm
 * cả dòng dễ đụng nhầm vào ô đó.
 */
@Composable
fun <T : PendingRow> PendingBatchList(
    rows: List<T>,
    onRemove: (Long) -> Unit,
    modifier: Modifier = Modifier,
    onRowClick: ((T) -> Unit)? = null,
    rowContent: @Composable (T) -> Unit,
) {
    LazyColumn(modifier = modifier) {
        items(rows, key = { it.key }) { row ->
            Card(modifier = Modifier.fillMaxWidth().padding(horizontal = 12.dp, vertical = 4.dp)) {
                Row(
                    modifier = Modifier.fillMaxWidth().padding(12.dp),
                    horizontalArrangement = Arrangement.SpaceBetween,
                    verticalAlignment = Alignment.CenterVertically,
                ) {
                    Column(modifier = Modifier.weight(1f)) {
                        rowContent(row)
                        if (row.status == PendingRowStatus.ERROR && row.error != null) {
                            Text(
                                row.error!!,
                                color = MaterialTheme.colorScheme.error,
                                style = MaterialTheme.typography.bodySmall,
                            )
                        }
                    }
                    if (onRowClick != null) {
                        IconButton(onClick = { onRowClick(row) }) {
                            Icon(Icons.Filled.Info, contentDescription = "Xem chi tiết")
                        }
                    }
                    when (row.status) {
                        PendingRowStatus.SAVING -> CircularProgressIndicator(
                            modifier = Modifier.size(20.dp),
                            strokeWidth = 2.dp,
                        )
                        else -> IconButton(onClick = { onRemove(row.key) }) {
                            Icon(Icons.Filled.Close, contentDescription = "Xoá")
                        }
                    }
                }
            }
        }
    }
}
