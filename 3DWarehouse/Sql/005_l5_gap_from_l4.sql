-- Khoảng trống lớn không phải ở L3-L4 (đã bỏ ở migration 004) mà là giữa L4 và L5 — anh xác nhận
-- lại: rộng bằng khoảng 4 cái kệ. GapBefore đặt ở L5 (kệ đứng SAU khoảng trống theo RackOrder).
UPDATE WH_Racks SET GapBefore = 340 WHERE WarehouseId = 'plantc' AND RackId = 'L5';
COMMIT;
