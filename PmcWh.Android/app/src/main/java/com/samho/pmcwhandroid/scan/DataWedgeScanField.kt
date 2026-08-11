package com.samho.pmcwhandroid.scan

import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.text.input.ImeAction
import kotlinx.coroutines.delay

/**
 * Ô nhập luôn giữ focus để nhận mã quét từ súng quét DataWedge — không mở camera nào cả. Gõ tay
 * rồi bấm Enter cũng hoạt động, tiện để test khi chưa có máy PDA thật trong tay.
 *
 * Máy Honeywell EDA51 thực tế đã test: súng quét bắn ký tự liên tục KHÔNG có Enter/Tab kết thúc,
 * nên không có ký tự nào đánh dấu "hết 1 mã" cả — nếu chỉ chờ Enter/Tab thì các mã quét dồn nhau sẽ
 * bị nối liền vào 1 chuỗi, không bao giờ commit. Cách xử lý: chờ 1 khoảng lặng ngắn
 * ([idleCommitMs]) sau ký tự cuối cùng rồi tự commit — súng quét gõ 1 mã trong vài chục mili giây,
 * trong khi gõ tay chậm hơn nhiều nên không lẫn. Vẫn giữ thêm việc bắt ký tự xuống dòng/tab (commit
 * ngay, không cần chờ) phòng trường hợp máy khác có cấu hình DataWedge gửi kèm ký tự kết thúc.
 *
 * [enabled] nên tắt khi có dialog khác đang mở trên màn hình (picker, xác nhận...) để tránh phím
 * gõ vào rơi lung tung không rõ đích. [refocusSignal] đổi giá trị (vd. đóng 1 dialog) để ép field
 * xin lại focus ngay cả khi [enabled] không đổi.
 */
@Composable
fun DataWedgeScanField(
    onScan: (String) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    label: String = "Quét mã hoặc nhập tay",
    refocusSignal: Any? = null,
    idleCommitMs: Long = 300L,
) {
    var text by remember { mutableStateOf("") }
    val focusRequester = remember { FocusRequester() }

    fun emit(raw: String) {
        val code = raw.trim()
        if (code.isNotEmpty()) onScan(code)
    }

    LaunchedEffect(enabled, refocusSignal) {
        if (enabled) focusRequester.requestFocus()
    }

    // LaunchedEffect(text) tự huỷ + chạy lại mỗi khi "text" đổi — nên delay() ở đây chỉ thật sự
    // hoàn tất khi KHÔNG có ký tự mới nào tới trong suốt idleCommitMs, tức là 1 lần quét đã xong.
    LaunchedEffect(text) {
        if (text.isNotEmpty()) {
            delay(idleCommitMs)
            emit(text)
            text = ""
        }
    }

    OutlinedTextField(
        value = text,
        onValueChange = { value ->
            // Quét dồn nhiều mã liên tiếp rất nhanh có thể khiến onValueChange chỉ được gọi 1 lần
            // với NHIỀU mã đã nối sẵn trong "value" trước khi Compose kịp recompose giữa 2 lần quét
            // — nếu máy có gửi kèm ký tự xuống dòng/tab thì tách hết theo TẤT CẢ ký tự đó (không chỉ
            // lấy mã đầu rồi bỏ phần còn lại), phần còn dư (chưa có ký tự kết thúc) để idle-commit lo.
            val segments = value.split('\n', '\t')
            if (segments.size > 1) {
                segments.dropLast(1).forEach { emit(it) }
                text = segments.last()
            } else {
                text = value
            }
        },
        enabled = enabled,
        singleLine = true,
        label = { Text(label) },
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Done),
        keyboardActions = KeyboardActions(onDone = { emit(text); text = "" }),
        modifier = modifier.focusRequester(focusRequester),
    )
}
