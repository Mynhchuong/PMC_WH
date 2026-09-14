-- Thêm bàn văn phòng (3 bộ PC, 2 máy in, 3 ghế) đứng trước R1, cách R1 khoảng 1 kệ.
-- GapBefore đặt ở R1 (kệ đứng SAU khoảng trống theo RackOrder) — R0 (order -1) là kệ đầu tiên,
-- không cần GapBefore vì không có gì đứng trước nó.
INSERT INTO WH_Racks (WarehouseId, RackId, RackOrder, Side, Label, Note, GapBefore)
VALUES ('plantc', 'R0', -1, 'R', 'OFFICE DESK', '3 bo PC, 2 may in, 3 ghe - cach R1 1 ke', 0);

UPDATE WH_Racks SET GapBefore = 100 WHERE WarehouseId = 'plantc' AND RackId = 'R1';
COMMIT;
