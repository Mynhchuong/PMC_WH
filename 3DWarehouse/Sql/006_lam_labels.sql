-- Bỏ chữ "MÁY " thừa trong tên máy Lamination, chỉ còn tiếng Anh.
UPDATE WH_Racks SET Label = 'LAMINATION MACHINE #4' WHERE WarehouseId = 'plantc' AND RackId = 'R18';
UPDATE WH_Racks SET Label = 'LAMINATION MACHINE #3' WHERE WarehouseId = 'plantc' AND RackId = 'R19';
UPDATE WH_Racks SET Label = 'LAMINATION MACHINE #2' WHERE WarehouseId = 'plantc' AND RackId = 'R20';
UPDATE WH_Racks SET Label = 'LAMINATION MACHINE #1' WHERE WarehouseId = 'plantc' AND RackId = 'R21';
COMMIT;
