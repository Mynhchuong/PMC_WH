-- Thêm GapBefore: khoảng trống PHỤ THÊM trước 1 kệ (so với khoảng cách chuẩn giữa 2 kệ liền
-- nhau) — dùng để thể hiện đúng các khoảng trống thật đã xác nhận trong bản vẽ layout (vd L4
-- cách L3 một khoảng lớn vì ngang hàng R8 qua lối đi; Laminate (L12) cách 3 cái bàn (L13) 1
-- khoảng). Đơn vị là "phần trăm của 1 khoảng cách chuẩn" (100 = thêm đúng 1 khoảng chuẩn nữa),
-- không phải đơn vị 3D thật vì chưa đo khoảng cách thật ngoài kho.
ALTER TABLE WH_Racks ADD (GapBefore NUMBER DEFAULT 0 NOT NULL);

UPDATE WH_Racks SET GapBefore = 150 WHERE WarehouseId = 'plantc' AND RackId = 'L4';
UPDATE WH_Racks SET GapBefore = 120 WHERE WarehouseId = 'plantc' AND RackId = 'L13';

COMMIT;
