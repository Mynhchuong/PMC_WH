package com.samho.pmcwhandroid.ui.components

import androidx.compose.material3.AlertDialog
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue

/**
 * Bọc [onExit] gốc lại thành 1 hàm mới: gọi vào sẽ hiện dialog hỏi xác nhận trước, chỉ thật sự
 * thoát khi bấm "Thoát". Dùng cho cả nút mũi tên trên TopAppBar lẫn [androidx.activity.compose.BackHandler]
 * (chỉ cần truyền hàm trả về từ đây vào onClick/onBack, không cần sửa gì khác ở 2 chỗ đó).
 *
 * PMC feedback: bấm nhầm mũi tên thoát giữa lúc đang quét dở lô hàng thì mất hết, phải quét lại từ
 * đầu — chỉ hiện cảnh báo khi đang CÓ việc dở dang ([hasPendingWork] = true), tránh làm phiền khi
 * chưa quét gì (không có gì để mất).
 */
@Composable
fun rememberExitConfirm(hasPendingWork: Boolean, onExit: () -> Unit): Pair<() -> Unit, @Composable () -> Unit> {
    var showConfirm by remember { mutableStateOf(false) }

    val guardedExit: () -> Unit = {
        if (hasPendingWork) showConfirm = true else onExit()
    }

    val dialog: @Composable () -> Unit = {
        if (showConfirm) {
            AlertDialog(
                onDismissRequest = { showConfirm = false },
                title = { Text("Thoát màn hình?") },
                text = { Text("Danh sách đang chờ lưu sẽ mất hết nếu thoát ra bây giờ.") },
                confirmButton = {
                    Button(onClick = { showConfirm = false; onExit() }) { Text("Thoát") }
                },
                dismissButton = {
                    TextButton(onClick = { showConfirm = false }) { Text("Ở lại") }
                },
            )
        }
    }

    return Pair(guardedExit, dialog)
}
