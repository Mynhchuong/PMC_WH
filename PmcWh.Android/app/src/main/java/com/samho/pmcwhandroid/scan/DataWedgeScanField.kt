package com.samho.pmcwhandroid.scan

import android.app.Activity
import android.content.Context
import android.content.ContextWrapper
import android.view.WindowManager
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material.icons.Icons
import androidx.compose.material.icons.filled.KeyboardHide
import androidx.compose.material.icons.filled.Search
import androidx.compose.material3.Icon
import androidx.compose.material3.IconButton
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import androidx.compose.ui.focus.FocusRequester
import androidx.compose.ui.focus.focusRequester
import androidx.compose.ui.focus.onFocusChanged
import androidx.compose.ui.platform.LocalSoftwareKeyboardController
import androidx.compose.ui.platform.LocalView
import androidx.compose.ui.text.input.ImeAction
import kotlinx.coroutines.delay

private tailrec fun Context.findActivity(): Activity? = when (this) {
    is Activity -> this
    is ContextWrapper -> baseContext.findActivity()
    else -> null
}

/**
 * Ô nhập luôn giữ focus để nhận mã quét từ súng quét DataWedge — không mở camera nào cả. Nhập tay
 * (bàn phím cứng của PDA) rồi bấm Enter hoặc bấm nút kính lúp bên phải cũng hoạt động.
 *
 * Máy Honeywell EDA51 thực tế đã test: súng quét bắn ký tự liên tục KHÔNG có Enter/Tab kết thúc,
 * nên không có ký tự nào đánh dấu "hết 1 mã" cả. Cách xử lý: chờ 1 khoảng lặng ngắn ([idleCommitMs])
 * sau ký tự cuối rồi tự commit.
 *
 * QUAN TRỌNG — phân biệt SÚNG QUÉT với GÕ TAY để idle-commit không cắt ngang lúc gõ tay:
 * súng quét bắn cả chuỗi trong vài mili giây (khoảng cách giữa 2 ký tự < [burstGapMs]), còn gõ tay
 * thì mỗi phím cách nhau hàng trăm mili giây. Nếu phát hiện có ít nhất 1 khoảng cách "chậm như gõ
 * tay" trong lần nhập hiện tại thì KHÔNG tự commit nữa — người dùng phải bấm Enter / nút kính lúp.
 * (Trước đây idle-commit bắn ngay sau 3-4 ký tự đầu khi gõ tay chậm → "không tìm thấy barcode".)
 * Vẫn giữ việc bắt ký tự xuống dòng/tab (commit ngay) phòng máy có cấu hình gửi kèm ký tự kết thúc.
 *
 * [enabled] nên tắt khi có dialog khác đang mở trên màn hình (picker, xác nhận, xem chi tiết...) để
 * tránh phím gõ vào rơi lung tung không rõ đích. [refocusSignal] đổi giá trị (vd. đóng 1 dialog) để
 * ép field xin lại focus ngay cả khi [enabled] không đổi.
 */
@Composable
fun DataWedgeScanField(
    onScan: (String) -> Unit,
    modifier: Modifier = Modifier,
    enabled: Boolean = true,
    label: String = "Quét mã hoặc nhập tay",
    refocusSignal: Any? = null,
    idleCommitMs: Long = 300L,
    minCommitLength: Int = 3,
    burstGapMs: Long = 80L,
) {
    var text by remember { mutableStateOf("") }
    // Dấu vết thời gian để phân biệt súng quét (chuỗi ký tự dồn dập) với gõ tay (chậm, rời rạc).
    var lastCharAt by remember { mutableStateOf(0L) }
    var sawSlowGap by remember { mutableStateOf(false) }
    val focusRequester = remember { FocusRequester() }
    val keyboardController = LocalSoftwareKeyboardController.current
    val view = LocalView.current

    fun emit(raw: String) {
        val code = raw.trim()
        if (code.isNotEmpty()) onScan(code)
        text = ""
        sawSlowGap = false
        lastCharAt = 0L
    }

    // keyboardController.hide() (dưới) chỉ ẨN bàn phím SAU KHI nó đã kịp bật — chạy đua với việc hệ
    // thống tự bật bàn phím ngay khi field nhận focus thì nhiều lúc hide() thua, bàn phím vẫn hiện
    // (thực tế đo trên máy Honeywell EDA51: xảy ra thường xuyên). Cách chặn triệt để: đổi
    // softInputMode của cửa sổ sang SOFT_INPUT_STATE_ALWAYS_HIDDEN suốt thời gian field này còn trên
    // màn hình — hệ thống sẽ không tự bật bàn phím khi focus nữa (field vẫn nhận được ký tự từ súng
    // quét bình thường, vì đó là input connection chứ không phải bàn phím ảo). Trả lại softInputMode
    // cũ lúc rời màn hình, vì màn Đăng nhập vẫn cần bàn phím thật để gõ user/password.
    DisposableEffect(Unit) {
        val window = view.context.findActivity()?.window
        val previousSoftInputMode = window?.attributes?.softInputMode
        window?.setSoftInputMode(WindowManager.LayoutParams.SOFT_INPUT_STATE_ALWAYS_HIDDEN)
        onDispose {
            if (window != null && previousSoftInputMode != null) {
                window.setSoftInputMode(previousSoftInputMode)
            }
        }
    }

    // Field này chỉ để nhận ký tự từ súng quét DataWedge (mô phỏng bàn phím vật lý), không phải để
    // gõ tay — vẫn cần giữ focus để nhận ký tự, nhưng PHẢI ẩn bàn phím ảo, không thì nó tự bật lên
    // che nửa màn hình mỗi khi field có focus. Ẩn cả lúc xin focus lẫn mỗi khi focus thay đổi (vd.
    // mở dialog rồi đóng lại, quay lại field cũng bị tự bật bàn phím lại).
    LaunchedEffect(enabled, refocusSignal) {
        if (enabled) {
            focusRequester.requestFocus()
            keyboardController?.hide()
        }
    }

    // LaunchedEffect(text) tự huỷ + chạy lại mỗi khi "text" đổi — nên delay() ở đây chỉ thật sự
    // hoàn tất khi KHÔNG có ký tự mới nào tới trong suốt idleCommitMs. Chỉ auto-commit khi chuỗi
    // đến theo kiểu súng quét (không có khoảng lặng chậm nào); gõ tay thì chờ Enter / nút kính lúp.
    LaunchedEffect(text) {
        if (text.length >= minCommitLength && !sawSlowGap) {
            delay(idleCommitMs)
            emit(text)
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
            val newText = if (segments.size > 1) {
                segments.dropLast(1).forEach { emit(it) }
                segments.last()
            } else {
                value
            }

            // Cập nhật dấu vết thời gian TRƯỚC khi set text (để LaunchedEffect(text) chạy lại đọc
            // đúng sawSlowGap mới). Chỉ tính khi có thêm ký tự (bỏ qua xoá bớt).
            if (newText.length > text.length) {
                val now = System.currentTimeMillis()
                if (text.isEmpty()) {
                    // Bắt đầu 1 lần nhập mới.
                    sawSlowGap = false
                } else if (now - lastCharAt > burstGapMs) {
                    sawSlowGap = true
                }
                lastCharAt = now
            }
            text = newText
        },
        enabled = enabled,
        singleLine = true,
        label = { Text(label) },
        keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
        keyboardActions = KeyboardActions(onSearch = { emit(text) }, onDone = { emit(text) }),
        trailingIcon = {
            if (text.isNotBlank()) {
                // Nút commit tường minh cho nhập tay — không phụ thuộc phím Enter của bàn phím cứng.
                IconButton(onClick = { emit(text) }) {
                    Icon(Icons.Filled.Search, contentDescription = "Tìm mã đã nhập")
                }
            } else {
                // Nút ẩn bàn phím khi nó lỡ bật lên — CHỈ ẩn bàn phím, KHÔNG bỏ focus của field, để
                // súng quét vẫn bắn ký tự vào được bình thường sau khi bấm nút này.
                IconButton(onClick = { keyboardController?.hide() }) {
                    Icon(Icons.Filled.KeyboardHide, contentDescription = "Ẩn bàn phím")
                }
            }
        },
        modifier = modifier
            .focusRequester(focusRequester)
            .onFocusChanged { state -> if (state.isFocused) keyboardController?.hide() },
    )
}
