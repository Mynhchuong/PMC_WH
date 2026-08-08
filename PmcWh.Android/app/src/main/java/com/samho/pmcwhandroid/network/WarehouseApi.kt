package com.samho.pmcwhandroid.network

import retrofit2.http.GET
import retrofit2.http.Path
import retrofit2.http.Query

interface WarehouseApi {
    @GET("api/Warehouse/layout")
    suspend fun layout(): List<WarehouseTierDto>

    @GET("api/Warehouse/location/{id}/materials")
    suspend fun locationMaterials(
        @Path("id") locationId: Int,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 8,
    ): PagedResult<LocationMaterialDto>

    @GET("api/Warehouse/today-log")
    suspend fun todayLog(
        @Query("movementType") movementType: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): PagedResult<TodayLogItem>
}
