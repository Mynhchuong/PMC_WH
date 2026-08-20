package com.samho.pmcwhandroid.network

import kotlinx.serialization.Serializable

@Serializable
data class BarcodeListDto(
    val listId: Int,
    val name: String,
    val createdAt: String = "",
    val itemCount: Int = 0,
    val totalScans: Int = 0,
)

@Serializable
data class BarcodeListItemDto(
    val itemId: Int,
    val barcode: String,
    val scanCount: Int,
    val lastScannedAt: String = "",
)

@Serializable
data class CreateBarcodeListRequest(
    val name: String,
    val userId: Int? = null,
)

@Serializable
data class ScanBarcodeRequest(
    val barcode: String,
)

@Serializable
data class ScanBarcodeResult(
    val isDuplicate: Boolean,
    val scanCount: Int,
)
