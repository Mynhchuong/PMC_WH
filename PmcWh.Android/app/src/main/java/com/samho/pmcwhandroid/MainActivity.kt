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
import com.samho.pmcwhandroid.ui.HomeScreen
import com.samho.pmcwhandroid.ui.LoginScreen
import com.samho.pmcwhandroid.ui.theme.PmcWhAndroidTheme

class MainActivity : ComponentActivity() {
    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        enableEdgeToEdge()

        val sessionManager = SessionManager(applicationContext)

        setContent {
            PmcWhAndroidTheme {
                var session by remember { mutableStateOf<UserSession?>(sessionManager.getSession()) }

                Scaffold(modifier = Modifier.fillMaxSize()) { innerPadding ->
                    val currentSession = session
                    if (currentSession == null) {
                        LoginScreen(
                            modifier = Modifier.padding(innerPadding),
                            onLoginSuccess = { result ->
                                sessionManager.save(result)
                                session = sessionManager.getSession()
                            },
                        )
                    } else {
                        HomeScreen(
                            session = currentSession,
                            modifier = Modifier.padding(innerPadding),
                            onLogout = {
                                sessionManager.clear()
                                session = null
                            },
                        )
                    }
                }
            }
        }
    }
}
