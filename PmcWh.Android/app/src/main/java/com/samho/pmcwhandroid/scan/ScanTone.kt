package com.samho.pmcwhandroid.scan

import android.content.Context
import android.media.AudioManager
import android.media.ToneGenerator
import android.os.Build
import android.os.Handler
import android.os.Looper
import android.os.VibrationEffect
import android.os.Vibrator
import android.os.VibratorManager

/**
 * Tiếng "bíp" + rung phản hồi khi quét — tách biệt SUCCESS (mã hợp lệ) và FAILURE (không có trong
 * CSDL / sai trạng thái / lỗi mạng...) để công nhân phân biệt được ngay bằng tai/tay, không cần
 * nhìn màn hình liên tục lúc quét dồn dập nhiều mã. Dùng [ToneGenerator] (âm hệ thống có sẵn của
 * Android, đúng cặp tone ACK/NACK vốn được thiết kế cho việc báo hiệu thành công/thất bại) — không
 * cần bundle file âm thanh riêng.
 *
 * FAILURE phát trên STREAM_ALARM (to nhất, gần như không bao giờ bị người dùng tắt/vặn nhỏ, khác
 * hẳn STREAM_NOTIFICATION mà máy thường để nhỏ/im lặng theo mặc định) + kéo dài hơn SUCCESS, cộng
 * thêm rung 2 nhịp — PMC phản hồi tiếng báo sai trước đó quá nhỏ, công nhân không nhận ra. SUCCESS
 * chỉ có tiếng, KHÔNG rung (rung mỗi lần quét đúng liên tục sẽ gây khó chịu khi quét dồn dập).
 */
object ScanTone {
    fun success() {
        play(ToneGenerator.TONE_PROP_ACK, AudioManager.STREAM_MUSIC, 180)
    }

    fun failure(context: Context) {
        play(ToneGenerator.TONE_PROP_NACK, AudioManager.STREAM_ALARM, 500)
        vibrate(context, longArrayOf(0, 140, 90, 140))
    }

    private fun play(tone: Int, streamType: Int, durationMs: Int) {
        try {
            val generator = ToneGenerator(streamType, 100)
            generator.startTone(tone, durationMs)
            // ToneGenerator giữ tài nguyên audio native — phải release() sau khi tiếng phát xong,
            // không thì rò rỉ dần qua mỗi lần quét.
            Handler(Looper.getMainLooper()).postDelayed({ generator.release() }, durationMs + 100L)
        } catch (_: Exception) {
            // Thiết bị hiếm gặp không hỗ trợ ToneGenerator — bỏ qua, không làm crash luồng quét.
        }
    }

    // pattern: [chờ, rung, nghỉ, rung, ...] tính bằng mili giây (giống format Vibrator cũ).
    private fun vibrate(context: Context, pattern: LongArray) {
        try {
            val vibrator = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.S) {
                val vm = context.getSystemService(Context.VIBRATOR_MANAGER_SERVICE) as? VibratorManager
                vm?.defaultVibrator
            } else {
                @Suppress("DEPRECATION")
                context.getSystemService(Context.VIBRATOR_SERVICE) as? Vibrator
            }
            if (vibrator == null || !vibrator.hasVibrator()) return
            if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
                vibrator.vibrate(VibrationEffect.createWaveform(pattern, -1))
            } else {
                @Suppress("DEPRECATION")
                vibrator.vibrate(pattern, -1)
            }
        } catch (_: Exception) {
            // Thiết bị không có motor rung hoặc bị chặn quyền — bỏ qua, không làm crash luồng quét.
        }
    }
}
