package com.samho.pmcwhandroid.network

import okhttp3.ResponseBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.DELETE
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path

interface BarcodeCollectionApi {
    @GET("api/BarcodeCollection/lists")
    suspend fun getLists(): List<BarcodeListDto>

    @POST("api/BarcodeCollection/lists")
    suspend fun createList(@Body request: CreateBarcodeListRequest): Response<BarcodeListDto>

    /** Api trả Ok() không có body khi thành công — dùng Response<ResponseBody> giống các endpoint
     *  xoá khác trong app, tránh SerializationException khi converter cố parse chuỗi rỗng. */
    @DELETE("api/BarcodeCollection/lists/{id}")
    suspend fun deleteList(@Path("id") id: Int): Response<ResponseBody>

    @GET("api/BarcodeCollection/lists/{id}/items")
    suspend fun getItems(@Path("id") id: Int): List<BarcodeListItemDto>

    @POST("api/BarcodeCollection/lists/{id}/scan")
    suspend fun scan(@Path("id") id: Int, @Body request: ScanBarcodeRequest): Response<ScanBarcodeResult>

    @DELETE("api/BarcodeCollection/lists/{id}/items/{itemId}")
    suspend fun deleteItem(@Path("id") id: Int, @Path("itemId") itemId: Int): Response<ResponseBody>
}
