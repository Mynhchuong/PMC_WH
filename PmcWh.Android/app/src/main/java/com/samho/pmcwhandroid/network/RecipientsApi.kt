package com.samho.pmcwhandroid.network

import retrofit2.http.GET
import retrofit2.http.Query

interface RecipientsApi {
    @GET("api/Recipients")
    suspend fun list(
        @Query("includeInactive") includeInactive: Boolean = false,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 100,
    ): PagedResult<RecipientDto>
}
