-- R18-R21 (máy Lamination) đang bị ẩn ở JS nên R17/R22 hiện dính sát nhau — thêm khoảng trống lại,
-- rộng cỡ 1 kệ, để giữ đúng cảm giác không gian thật (dù mắt không thấy 4 máy đó).
UPDATE WH_Racks SET GapBefore = 100 WHERE WarehouseId = 'plantc' AND RackId = 'R22';
COMMIT;
