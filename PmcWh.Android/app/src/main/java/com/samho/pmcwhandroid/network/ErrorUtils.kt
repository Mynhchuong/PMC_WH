package com.samho.pmcwhandroid.network

import kotlinx.serialization.json.Json
import retrofit2.Response

private val errorJson = Json { ignoreUnknownKeys = true; isLenient = true }

/** Api trả lỗi nghiệp vụ dạng {"message": "..."} — đọc ra để hiện đúng lý do thay vì lỗi chung chung.
 *  Toàn bộ nằm trong try/catch (kể cả .string() đọc stream) — hàm này không bao giờ được ném exception
 *  ra ngoài, vì nó thường được gọi ngay trong nhánh xử lý lỗi, lỗi chồng lỗi dễ crash app. */
fun <T> Response<T>.errorMessageOrDefault(default: String): String {
    return try {
        val body = errorBody()?.string()
        if (body.isNullOrBlank()) default
        else errorJson.decodeFromString(ApiMessage.serializer(), body).message ?: default
    } catch (e: Exception) {
        default
    }
}
