package com.samho.pmcwhandroid.network

import com.jakewharton.retrofit2.converter.kotlinx.serialization.asConverterFactory
import com.samho.pmcwhandroid.BuildConfig
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.logging.HttpLoggingInterceptor
import retrofit2.Retrofit

object ApiClient {
    /** Token của phiên đang đăng nhập — MainActivity set sau khi login/khôi phục phiên, xóa khi logout.
     *  Các endpoint hiện tại (nhập/xuất/hủy...) chưa bắt buộc token, nhưng gắn sẵn cho phase sau. */
    var authToken: String? = null

    private val json = Json {
        ignoreUnknownKeys = true
        isLenient = true
    }

    private val authInterceptor = okhttp3.Interceptor { chain ->
        val token = authToken
        val request = if (token.isNullOrBlank()) {
            chain.request()
        } else {
            chain.request().newBuilder().addHeader("Authorization", "Bearer $token").build()
        }
        chain.proceed(request)
    }

    private val okHttpClient = OkHttpClient.Builder()
        .addInterceptor(authInterceptor)
        .apply {
            // Log đầy đủ request/response (kể cả mật khẩu lúc login) — CHỈ bật ở debug build,
            // build release không được lộ ra Logcat.
            if (BuildConfig.DEBUG) {
                addInterceptor(HttpLoggingInterceptor().apply { level = HttpLoggingInterceptor.Level.BODY })
            }
        }
        .build()

    private val retrofit = Retrofit.Builder()
        .baseUrl(ApiConfig.BASE_URL)
        .client(okHttpClient)
        .addConverterFactory(json.asConverterFactory("application/json".toMediaType()))
        .build()

    val authApi: AuthApi = retrofit.create(AuthApi::class.java)
    val materialsApi: MaterialsApi = retrofit.create(MaterialsApi::class.java)
    val recipientsApi: RecipientsApi = retrofit.create(RecipientsApi::class.java)
    val warehouseApi: WarehouseApi = retrofit.create(WarehouseApi::class.java)
    val barcodeCollectionApi: BarcodeCollectionApi = retrofit.create(BarcodeCollectionApi::class.java)
}
