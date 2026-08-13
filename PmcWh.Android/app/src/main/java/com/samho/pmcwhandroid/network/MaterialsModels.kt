package com.samho.pmcwhandroid.network

import kotlinx.serialization.Serializable

@Serializable
data class MaterialListItem(
    val materialId: Int,
    val barcode: String,
    val dev: String? = null,
    val poNo: String? = null,
    val supplier: String? = null,
    val model: String? = null,
    val colorway: String? = null,
    val sizeSpec: String? = null,
    val matlDescription: String? = null,
    val colorCode: String? = null,
    val season: String? = null,
    val stage: String? = null,
    val arrivalQty: Double? = null,
    val balance: Double? = null,
    val unit: String? = null,
    val status: String = "",
    val locationCode: String? = null,
    val isOverdue: Boolean = false,
)

@Serializable
data class PagedResult<T>(
    val items: List<T> = emptyList(),
    val page: Int = 1,
    val pageSize: Int = 20,
    val totalCount: Int = 0,
    val totalPages: Int = 0,
)

@Serializable
data class StorageLocationDto(
    val locationId: Int,
    val rackNo: Int,
    val levelNo: Int,
    val code: String,
)

@Serializable
data class InboundRequest(
    val locationId: Int,
    val userId: Int,
)

@Serializable
data class IssueRequest(
    val recipientId: Int,
    val qty: Double,
    val userId: Int,
)

@Serializable
data class RecipientDto(
    val recipientId: Int,
    val name: String,
    val isActive: Boolean = true,
)

@Serializable
data class DisposeRequest(
    val userId: Int,
)

@Serializable
data class WarehouseTierDto(
    val locationId: Int,
    val rackNo: Int,
    val levelNo: Int,
    val code: String,
    val qrCount: Int = 0,
)

@Serializable
data class LocationMaterialDto(
    val materialId: Int,
    val barcode: String,
    val dev: String? = null,
    val model: String? = null,
    val sizeSpec: String? = null,
    val balance: Double? = null,
    val unit: String? = null,
)

@Serializable
data class TodayLogItem(
    val movementId: Int,
    val materialId: Int,
    val barcode: String,
    val dev: String? = null,
    val model: String? = null,
    val movementType: String = "",
    val qty: Double = 0.0,
    val unit: String? = null,
    val locationCode: String? = null,
    val username: String? = null,
    val recipientName: String? = null,
    val note: String? = null,
    val occurredAt: String = "",
)

@Serializable
data class ApiMessage(
    val message: String? = null,
)
