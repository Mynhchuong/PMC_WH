package com.samho.pmcwhandroid.network

import retrofit2.http.Body
import retrofit2.http.POST

interface AuthApi {
    @POST("api/Users/authenticate")
    suspend fun authenticate(@Body request: LoginRequest): LoginResult
}
