package com.samho.pmcwhandroid.network

/**
 * PmcWh.Api chạy trên máy dev/server nội bộ, không phải trên máy PDA — phải trỏ
 * đúng địa chỉ LAN của máy đó, "localhost" trên điện thoại sẽ trỏ vào chính điện thoại.
 *
 * - Test bằng Android Emulator: giữ nguyên "10.0.2.2" (alias đặc biệt trỏ về máy host).
 * - Test bằng máy PDA/điện thoại thật: đổi thành IP LAN của máy chạy PmcWh.Api
 *   (vd. "192.168.1.233"), và máy đó phải chạy Api với --urls http://0.0.0.0:5073
 *   (mặc định "localhost" chỉ nhận kết nối từ chính máy đó, thiết bị khác sẽ không gọi được).
 */
object ApiConfig {
    const val BASE_URL = "http://10.0.2.2:5073/"
}
