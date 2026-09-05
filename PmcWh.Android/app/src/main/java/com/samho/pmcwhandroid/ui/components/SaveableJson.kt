package com.samho.pmcwhandroid.ui.components

import androidx.compose.runtime.saveable.Saver
import kotlinx.serialization.KSerializer
import kotlinx.serialization.builtins.ListSerializer
import kotlinx.serialization.json.Json

/**
 * Saver cho rememberSaveable dựa trên kotlinx-serialization — để các danh sách "chờ lưu" (pending)
 * và lựa chọn kèm theo (người nhận / kệ đang chọn) SỐNG SÓT khi Android giết app ở nền hoặc xoay
 * máy. Trước đây chúng nằm trong remember thường nên process-death là mất trắng cả lô đang quét dở
 * (đúng loại lỗi PMC đã than phiền), mà nút "thoát có xác nhận" cũng không cứu được vì hệ thống tự
 * giết chứ không phải người dùng bấm back.
 *
 * Lưu xuống Bundle dạng chuỗi JSON. decode lỗi (dữ liệu cũ / khác phiên) thì trả rỗng / null thay
 * vì ném exception làm crash ngay lúc khôi phục.
 */
private val saverJson = Json { ignoreUnknownKeys = true }

/** Saver cho 1 List<T> serializable — dùng với `rememberSaveable(stateSaver = ...)`. */
fun <T> listJsonSaver(elementSerializer: KSerializer<T>): Saver<List<T>, String> {
    val listSerializer = ListSerializer(elementSerializer)
    return Saver(
        save = { runCatching { saverJson.encodeToString(listSerializer, it) }.getOrDefault("[]") },
        restore = { runCatching { saverJson.decodeFromString(listSerializer, it) }.getOrDefault(emptyList()) },
    )
}

/** Saver cho 1 object serializable có thể null — dùng với `rememberSaveable(stateSaver = ...)`. */
fun <T : Any> nullableJsonSaver(serializer: KSerializer<T>): Saver<T?, String> = Saver(
    save = { value -> if (value == null) "" else runCatching { saverJson.encodeToString(serializer, value) }.getOrDefault("") },
    restore = { text -> if (text.isBlank()) null else runCatching { saverJson.decodeFromString(serializer, text) }.getOrNull() },
)
