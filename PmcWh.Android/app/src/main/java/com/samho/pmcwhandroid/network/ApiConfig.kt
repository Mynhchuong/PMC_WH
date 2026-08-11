package com.samho.pmcwhandroid.network

/**
 * Server chính thức: PmcWh.Api host trên IIS tại 192.168.1.24, dưới virtual path "/PmcWh.Api"
 * (không phải máy dev chạy dotnet run) — mobile/PDA thật luôn trỏ về đây.
 *
 * Nếu cần test tạm bằng máy dev (dotnet run) thay vì server IIS:
 * - Android Emulator: "http://10.0.2.2:5073/" (alias đặc biệt trỏ về máy host).
 * - PDA/điện thoại thật cùng LAN với máy dev: IP LAN của máy đó, vd "http://192.168.1.233:5073/",
 *   và máy đó phải chạy Api với --urls http://0.0.0.0:5073 (mặc định "localhost" chỉ nhận kết nối
 *   từ chính máy đó, thiết bị khác sẽ không gọi được).
 */
object ApiConfig {
    const val BASE_URL = "http://192.168.1.24/PmcWh.Api/"
}
