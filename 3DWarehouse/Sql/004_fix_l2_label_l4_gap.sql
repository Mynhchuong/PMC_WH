-- L2: bỏ chữ "BÀNG" thừa trong tên, chỉ còn "COMPONEMT INSPECTION" (theo yêu cầu anh).
-- L4: khoảng cách với L3 là BÌNH THƯỜNG, không phải khoảng lớn — bỏ GapBefore đã set nhầm trước đó.
UPDATE WH_Racks SET Label = 'COMPONEMT INSPECTION' WHERE WarehouseId = 'plantc' AND RackId = 'L2';
UPDATE WH_Racks SET GapBefore = 0 WHERE WarehouseId = 'plantc' AND RackId = 'L4';
COMMIT;
