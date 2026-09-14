-- WH_Racks / WH_RackLocations — bảng kệ + vị trí DÙNG CHUNG cho nhiều kho (không riêng PMC),
-- phân biệt bằng cột WarehouseId (khớp Id trong Helpers/WarehouseRegistry.cs, vd 'plantc').
-- Khác PMC_StorageLocations (1 tầng = 1 vị trí duy nhất): ở đây 1 tầng có NHIỀU ô, mỗi ô 1 mã
-- riêng (Face + LevelNo + CellNo) — đúng theo cách Plant C đặt mã (NB220-11, NB220-21, ...).
-- Viết theo chuẩn Oracle 10g (CLAUDE.md gốc): không dùng IDENTITY/OFFSET-FETCH, PK là khoá tự nhiên.

CREATE TABLE WH_Racks (
  WarehouseId   VARCHAR2(20)   NOT NULL,
  RackId        VARCHAR2(20)   NOT NULL,
  RackOrder     NUMBER         NOT NULL,
  Side          VARCHAR2(10)   NOT NULL,   -- 'L' / 'R' — dãy trái/phải quanh lối đi chính
  Label         VARCHAR2(200),
  Note          VARCHAR2(500),
  IsActive      NUMBER(1)      DEFAULT 1 NOT NULL,
  CONSTRAINT PK_WH_Racks PRIMARY KEY (WarehouseId, RackId)
);

CREATE TABLE WH_RackLocations (
  WarehouseId   VARCHAR2(20)   NOT NULL,
  RackId        VARCHAR2(20)   NOT NULL,
  Face          VARCHAR2(10)   NOT NULL,   -- 'FRONT' / 'BACK' — kệ 1 mặt thì chỉ có FRONT
  LevelNo       NUMBER         NOT NULL,   -- tầng, 1 = sát đất, tăng dần lên cao
  CellNo        NUMBER         NOT NULL,   -- số thứ tự ô trong tầng đó, 1 = ô đầu
  LocationCode  VARCHAR2(50),              -- mã vị trí thật, vd NB220-11 — NULL = ô còn trống, chưa gán mã
  CONSTRAINT PK_WH_RackLocations PRIMARY KEY (WarehouseId, RackId, Face, LevelNo, CellNo),
  CONSTRAINT FK_WH_RackLocations_Rack FOREIGN KEY (WarehouseId, RackId)
      REFERENCES WH_Racks (WarehouseId, RackId)
);

CREATE INDEX IX_WH_RackLocations_Code ON WH_RackLocations (LocationCode);
