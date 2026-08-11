package com.samho.pmcwhandroid.ui.theme

import android.app.Activity
import android.os.Build
import androidx.compose.foundation.isSystemInDarkTheme
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.darkColorScheme
import androidx.compose.material3.dynamicDarkColorScheme
import androidx.compose.material3.dynamicLightColorScheme
import androidx.compose.material3.lightColorScheme
import androidx.compose.runtime.Composable
import androidx.compose.ui.graphics.Color
import androidx.compose.ui.platform.LocalContext

// Màu thương hiệu cố định (khớp logo/launcher icon + login) — không dùng Material You mặc định
// vì mỗi máy Android 12+ sẽ tự đổi theo hình nền, làm mất nhận diện thương hiệu.
private val DarkColorScheme = darkColorScheme(
    primary = BrandSlate90,
    onPrimary = OnBrandDark,
    primaryContainer = BrandSlateContainerDark,
    onPrimaryContainer = OnBrandLight,
    secondary = BrandGray90,
    onSecondary = OnBrandDark,
    secondaryContainer = BrandGrayContainerDark,
    onSecondaryContainer = OnBrandLight,
    tertiary = BrandAmber90,
    onTertiary = Color(0xFF4D3800),
    tertiaryContainer = BrandAmberContainerDark,
    onTertiaryContainer = BrandAmberContainerLight,
)

private val LightColorScheme = lightColorScheme(
    primary = BrandSlateLight,
    onPrimary = Color.White,
    primaryContainer = BrandSlateContainerLight,
    onPrimaryContainer = OnBrandDark,
    secondary = BrandGrayLight,
    onSecondary = Color.White,
    secondaryContainer = BrandGrayContainerLight,
    onSecondaryContainer = BrandSlateLight,
    tertiary = BrandAmberLight,
    onTertiary = Color.White,
    tertiaryContainer = BrandAmberContainerLight,
    onTertiaryContainer = Color(0xFF4D3800),
)

@Composable
fun PmcWhAndroidTheme(
    darkTheme: Boolean = isSystemInDarkTheme(),
    // Mặc định TẮT Material You — giữ đúng màu thương hiệu trên mọi máy thay vì đổi theo
    // hình nền từng máy (Android 12+), khớp với logo/launcher icon/login đã cố định màu.
    dynamicColor: Boolean = false,
    content: @Composable () -> Unit
) {
    val colorScheme = when {
        dynamicColor && Build.VERSION.SDK_INT >= Build.VERSION_CODES.S -> {
            val context = LocalContext.current
            if (darkTheme) dynamicDarkColorScheme(context) else dynamicLightColorScheme(context)
        }

        darkTheme -> DarkColorScheme
        else -> LightColorScheme
    }

    MaterialTheme(
        colorScheme = colorScheme,
        typography = Typography,
        content = content
    )
}