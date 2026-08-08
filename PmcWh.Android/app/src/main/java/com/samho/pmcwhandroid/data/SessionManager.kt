package com.samho.pmcwhandroid.data

import android.content.Context
import com.samho.pmcwhandroid.network.LoginResult

data class UserSession(
    val userId: Int,
    val username: String,
    val fullName: String?,
    val role: String,
    val token: String?,
)

/** Lưu phiên đăng nhập vào SharedPreferences để mở lại app không phải login lại mỗi lần.
 *  Token JWT lưu kèm để các lần gọi Api sau (nhập/xuất/hủy...) gắn vào header Authorization. */
class SessionManager(context: Context) {
    private val prefs = context.getSharedPreferences("pmc_session", Context.MODE_PRIVATE)

    fun save(result: LoginResult) {
        prefs.edit()
            .putInt(KEY_USER_ID, result.userId)
            .putString(KEY_USERNAME, result.username)
            .putString(KEY_FULL_NAME, result.fullName)
            .putString(KEY_ROLE, result.role)
            .putString(KEY_TOKEN, result.token)
            .apply()
    }

    fun getSession(): UserSession? {
        val username = prefs.getString(KEY_USERNAME, null) ?: return null
        return UserSession(
            userId = prefs.getInt(KEY_USER_ID, 0),
            username = username,
            fullName = prefs.getString(KEY_FULL_NAME, null),
            role = prefs.getString(KEY_ROLE, "") ?: "",
            token = prefs.getString(KEY_TOKEN, null),
        )
    }

    fun clear() {
        prefs.edit().clear().apply()
    }

    private companion object {
        const val KEY_USER_ID = "userId"
        const val KEY_USERNAME = "username"
        const val KEY_FULL_NAME = "fullName"
        const val KEY_ROLE = "role"
        const val KEY_TOKEN = "token"
    }
}
