package com.samho.pmcwhandroid

import android.os.Bundle
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.enableEdgeToEdge
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Scaffold
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Modifier
import com.samho.pmcwhandroid.data.SessionManager
import com.samho.pmcwhandroid.data.UserSession
import com.samho.pmcwhandroid.network.ApiClient
import com.samho.pmcwhandroid.ui.HomeScreen
import com.samho.pmcwhandroid.ui.LoginScreen
import com.samho.pmcwhandroid.ui.NhapKhoScreen
import com.samho.pmcwhandroid.ui.DanhSachKeScreen
import com.samho.pmcwhandroid.ui.HuyLieuScreen
import com.samho.pmcwhandroid.ui.LogHomNayScreen
import com.samho.pmcwhandroid.ui.TimKiemScreen
import com.samho.pmcwhandroid.ui.XuatKhoScreen
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        val sessionManager = SessionManager(applicationContext)
        // Mở lại app khi còn phiên cũ (chưa logout) — khôi phục luôn token cho ApiClient.
        ApiClient.authToken = sessionManager.getSession()?.token

        setContent {
            PmcWhAndroidTheme {
                var session by remember { mutableStateOf<UserSession?>(sessionManager.getSession()) }
                // Điều hướng đơn giản bằng route dạng chuỗi (khớp HomeMenuItem.id) — chưa cần
                // Navigation Compose vì độ sâu màn hình còn ít, "home" = null.
                var route by remember { mutableStateOf<String?>(null) }

                Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
                    val currentSession = session
                    if (currentSession == null) {
                        LoginScreen(
                            modifier = Modifier.padding(innerPadding),
                            onLoginSuccess = { result ->
                                sessionManager.save(result)
                                ApiClient.authToken = result.token
                                session = sessionManager.getSession()
                            },
                        )
                    } else {
                        when (route) {
                            "nhap_kho" -> NhapKhoScreen(
                                session = currentSession,
                                onBack = { route = null },
                            )
                            "xuat_kho" -> XuatKhoScreen(
                                session = currentSession,
                                onBack = { route = null },
                            )
                            "huy_lieu" -> HuyLieuScreen(
                                session = currentSession,
                                onBack = { route = null },
                            )
                            "tim_kiem" -> TimKiemScreen(onBack = { route = null })
                            "danh_sach_ke" -> DanhSachKeScreen(
                                session = currentSession,
                                onBack = { route = null },
                            )
                            "log_hom_nay" -> LogHomNayScreen(onBack = { route = null })
                            else -> HomeScreen(
                                session = currentSession,
                                modifier = Modifier.padding(innerPadding),
                                onLogout = {
                                    sessionManager.clear()
                                    ApiClient.authToken = null
                                    session = null
                                    route = null
                                },
                                onNavigate = { id -> route = id },
                            )
                        }
                    }
                }
            }
        }
    }
}
