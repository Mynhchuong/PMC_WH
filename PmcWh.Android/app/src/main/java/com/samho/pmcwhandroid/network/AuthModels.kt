package com.samho.pmcwhandroid.network

import kotlinx.serialization.Serializable

@Serializable
data class LoginRequest(
    val username: String,
    val password: String,
)

@Serializable
data class LoginResult(
    val success: Boolean = false,
    val message: String? = null,
    val userId: Int = 0,
    val username: String = "",
    val fullName: String? = null,
    val role: String = "",
)
