package com.samho.pmcwhandroid.network

import okhttp3.ResponseBody
import retrofit2.Response
import retrofit2.http.Body
import retrofit2.http.GET
import retrofit2.http.POST
import retrofit2.http.Path
import retrofit2.http.Query

interface MaterialsApi {
    @GET("api/Materials")
    suspend fun list(
        @Query("status") status: String? = null,
        @Query("barcode") barcode: String? = null,
        @Query("page") page: Int = 1,
        @Query("pageSize") pageSize: Int = 20,
    ): PagedResult<MaterialListItem>

    /** 404 nếu không thấy barcode — dùng Response<> để đọc message lỗi thay vì ném exception. */
    @GET("api/Materials/by-barcode/{barcode}")
    suspend fun getByBarcode(@Path("barcode") barcode: String): Response<MaterialListItem>

    /** Api trả Ok() KHÔNG có body (0 byte) khi thành công — phải dùng Response<ResponseBody> (raw,
     *  Retrofit bỏ qua converter) chứ không phải Response<Unit>, nếu không converter kotlinx-serialization
     *  sẽ cố parse chuỗi rỗng thành JSON và ném SerializationException dù request đã thành công thật. */
    @POST("api/Materials/{id}/inbound")
    suspend fun inbound(@Path("id") id: Int, @Body request: InboundRequest): Response<ResponseBody>

    /** LƯU Ý: 3 endpoint issuable/returnable/disposable trả về PagedResult (object {items:[...]},
     *  KHÔNG phải mảng) và mặc định pageSize=10 phía server. Khai báo sai kiểu = parse lỗi = danh
     *  sách rỗng. Các màn quét (Xuất/Nhận lại/Hủy) không dùng mấy cái này nữa — chúng tra từng mã
     *  qua getByBarcode() cho chuẩn; giữ lại đây phòng khi cần màn danh sách sau này. */
    @GET("api/Materials/issuable")
    suspend fun issuable(@Query("page") page: Int = 1, @Query("pageSize") pageSize: Int = 20): PagedResult<MaterialListItem>

    /** Cùng lý do dùng Response<ResponseBody> như inbound() ở trên — Ok() không có body. */
    @POST("api/Materials/{id}/issue")
    suspend fun issue(@Path("id") id: Int, @Body request: IssueRequest): Response<ResponseBody>

    @GET("api/Materials/returnable")
    suspend fun returnable(@Query("page") page: Int = 1, @Query("pageSize") pageSize: Int = 20): PagedResult<MaterialListItem>

    /** Tên "returnMaterial" (không phải "return") vì return là từ khoá Kotlin. Cùng lý do dùng
     *  Response<ResponseBody> như inbound()/issue() ở trên — Ok() không có body. */
    @POST("api/Materials/{id}/return")
    suspend fun returnMaterial(@Path("id") id: Int, @Body request: ReturnRequest): Response<ResponseBody>

    @GET("api/Materials/disposable")
    suspend fun disposable(@Query("page") page: Int = 1, @Query("pageSize") pageSize: Int = 20): PagedResult<MaterialListItem>

    /** Cùng lý do dùng Response<ResponseBody> như inbound()/issue() — Ok() không có body. */
    @POST("api/Materials/{id}/dispose")
    suspend fun dispose(@Path("id") id: Int, @Body request: DisposeRequest): Response<ResponseBody>

    @GET("api/StorageLocations")
    suspend fun storageLocations(): List<StorageLocationDto>
}
