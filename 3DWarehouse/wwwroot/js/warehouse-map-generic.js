// Renderer 3D CHUNG cho schema "generic" (WH_Racks/WH_RackLocations) — style dựng kệ CỐ TÌNH
// giống hệt warehouse-map.js (khung sắt: cột + giằng chéo + dầm + mặt sàn từng tầng + pallet/thùng
// hàng + bảng tên nổi phía trên), chỉ khác ở chỗ toạ độ TỰ SINH từ Side/Order/GapBefore thay vì đo
// tay từng kệ như PMC (Plant C chưa có bản đo đạc thật ngoài kho).
//
// Trục: Z = dọc theo lối đi chính (thứ tự RackOrder, có cộng thêm GapBefore cho các khoảng trống
// đã xác nhận trong bản vẽ layout — vd L4 cách L3 xa vì ngang hàng R8, Laminate cách 3 bàn 1
// khoảng). X = từ lối đi ra 2 bên (trái âm/phải dương). Y = chiều cao, tầng 1 sát đất tăng dần lên.
//
// QUAN TRỌNG: mặt rộng của kệ (chứa các ô 1..N) nằm DỌC THEO X (từ lối đi lớn ra tới vách), KHÔNG
// nằm dọc theo Z — vì Z là hướng "đi xa dần xuống dãy", nếu mặt rộng nằm theo Z thì nhìn dọc hành
// lang sẽ chỉ thấy cạnh mỏng của kệ (như cột đứng "|"), đúng cái anh chê. Đặt mặt rộng theo X thì
// dù đứng ở góc nào nhìn xuống hành lang, mặt kệ vẫn luôn quay ngang ra — đúng ý "kệ phải xoay
// ngang". Hệ quả: ô1 (gần lối đi lớn nhất) nằm ở đầu X gần lối đi, số ô tăng dần ra xa về phía
// vách — khớp đúng ảnh Excel anh gửi. RackOrder/GapBefore vẫn quyết định vị trí theo Z như cũ,
// chỉ là khoảng cách Z giữa 2 kệ giờ tính theo ĐỘ DÀY thật của kệ (Depth 1 mặt/2 mặt) thay vì 1
// PITCH cố định, vì "chiều dài" (mặt rộng, chứa ô) mới là thứ cố định bằng nhau, không phải độ dày.
(function () {
  'use strict';

  var I18N = window.PmcWhI18n || { t: function (k) { return k; }, onChange: function () {} };

  var dataEl = document.getElementById('whg-racks-data');
  var RACKS = dataEl ? JSON.parse(dataEl.textContent || '[]') : [];

  var wrap = document.getElementById('whg-wrap');
  var canvas = document.getElementById('whg-canvas');
  if (!wrap || !canvas) return;

  // ---------- hằng số kích thước — cùng tỉ lệ với warehouse-map.js (RACK_W=96/RACK_D=42/LEVEL_H=46) ----------
  var RACK_SPAN = 440;      // mặt rộng 1 kệ (dọc trục X, chứa các ô) — cố định, khớp "chiều dài all
                             // kệ đều ngang nhau" — 78→150→220→440 (x2), tăng dần cho ô đủ chỗ chứa
                             // 1 thùng hàng thật (carton) mà không bị chật. KHÔNG thêm vách ngăn
                             // giữa các ô — sếp chê nhìn giống nhiều kệ nhỏ ghép lại thành 1 kệ lớn;
                             // đúng kiểu thật là 1 kệ = 1 khung riêng (cột 2 đầu), hàng để mở trên
                             // mặt sàn.
  var GAP_UNIT = 110;       // 1 "đơn vị" GapBefore = 1 lối đi cỡ vừa (cộng thêm vào khoảng cách Z
                             // giữa 2 kệ) — xem 003_add_gap_before.sql
  var POST = 9, BEAM_H = 10, BASE_Y = 10; // dày hơn bản trước (5/6) cho chắc/đẹp, đỡ mảnh
  var TOTAL_RACK_H = 200; // TẤT CẢ kệ cao bằng nhau (anh xác nhận lại) — dù số tầng khác nhau,
                           // chiều cao 1 tầng (levelH, tính riêng từng kệ = (TOTAL_RACK_H-14)/số
                           // tầng) co giãn ngược lại để tổng chiều cao luôn ra TOTAL_RACK_H.
  var PICK_AISLE_W = 98; // bề rộng lối đi sát mặt trước mỗi kệ, chạy từ ô1 tới ô cuối — 20 rồi 40
                          // đều còn mảnh, tăng tiếp lên 65 cho rõ hẳn
  var DEPTH_1FACE = 42, DEPTH_2FACE = 76; // độ dày kệ dọc Z (1 mặt / 2 mặt áp lưng)
  var AISLE_HALF = 70;
  var CRATE_H = 22, PALLET_H = 4;

  function maxCell(levels) { var m = 0; (levels || []).forEach(function (l) { if (l.cellNo > m) m = l.cellNo; }); return m; }
  function maxLevel(levels) { var m = 0; (levels || []).forEach(function (l) { if (l.levelNo > m) m = l.levelNo; }); return m; }
  function byLevel(levels) {
    var map = {};
    (levels || []).forEach(function (l) { (map[l.levelNo] = map[l.levelNo] || []).push(l); });
    Object.keys(map).forEach(function (k) { map[k].sort(function (a, b) { return a.cellNo - b.cellNo; }); });
    return map;
  }

  // ---------- gán toạ độ Z tích luỹ theo Order + GapBefore, cộng dồn theo ĐỘ DÀY THẬT từng kệ ----------
  function thicknessOf(hasBack) { return hasBack ? DEPTH_2FACE : DEPTH_1FACE; }
  var HIDDEN_RACKS = { L13: true, R18: true, R19: true, R20: true, R21: true }; // ẩn tạm
  var placed = { L: [], R: [] };
  ['L', 'R'].forEach(function (side) {
    var list = RACKS.filter(function (r) { return r.side === side && !HIDDEN_RACKS[r.rackId]; }).sort(function (a, b) { return a.order - b.order; });
    var z = 0, prevHalfThick = 0;
    list.forEach(function (r, idx) {
      var levels = Math.max(1, maxLevel(r.front), maxLevel(r.back));
      var cols = Math.max(1, maxCell(r.front), maxCell(r.back));
      var hasBack = (r.back || []).length > 0;
      var thick = thicknessOf(hasBack);
      // Chừa đúng PICK_AISLE_W làm lối lấy hàng thật (có bề rộng để đứng), không phải khe SEAM
      // mảnh nữa — kệ liền kề giờ không dính sát 100% được nữa vì lối lấy hàng cần chỗ đứng thật.
      if (idx > 0) z += prevHalfThick + PICK_AISLE_W + (r.gapBefore || 0) / 100 * GAP_UNIT + thick / 2;
      var isLam = /LAMINATION|FINISH/i.test(r.label || '');
      var isPrint = /PRINTING/i.test(r.label || '');
      var isTable = !isPrint && /BÀNG|BANG|TABLE|INSPECTION/i.test(r.label || '');
      placed[side].push({
        rack: r, levels: levels, cols: cols, hasBack: hasBack, thick: thick,
        kind: isLam ? 'lam' : (isPrint ? 'print' : (isTable ? 'table' : 'rack')),
        z: z,
      });
      prevHalfThick = thick / 2;
    });
  });

  // lối đi nhỏ giữa MỌI cặp kệ liền kề trong cùng 1 dãy — CHỈ vẽ khi khoảng hở thật sự lớn hơn
  // khe SEAM mặc định (do GapBefore, vd trước L4 / trước 3 bàn), xem vòng lặp render bên dưới.
  var smallWalkways = [];
  ['L', 'R'].forEach(function (side) {
    var list = placed[side];
    for (var i = 0; i < list.length - 1; i++) {
      var a = list[i], b = list[i + 1];
      var gapStart = a.z + a.thick / 2, gapEnd = b.z - b.thick / 2;
      smallWalkways.push({ side: side, z: (gapStart + gapEnd) / 2, len: gapEnd - gapStart });
    }
  });

  // Lấy đúng MÉP SAU thật của kệ cuối (z + nửa độ dày) làm mốc cuối mặt bằng — trước đó cộng thêm
  // nguyên RACK_SPAN (440, kích thước mặt rộng — trục KHÁC hẳn) làm dư ra 1 khoảng trống rất lớn
  // phía sau R22/cuối dãy trái, khiến cổng và viền tường nằm cách xa kệ cuối chứ không sát nữa.
  var lastL = placed.L.length ? placed.L[placed.L.length - 1] : null;
  var lastR = placed.R.length ? placed.R[placed.R.length - 1] : null;
  var totalLen = Math.max(
    lastL ? lastL.z + lastL.thick / 2 : 0,
    lastR ? lastR.z + lastR.thick / 2 : 0
  );

  // Khai báo SỚM (trước khi vẽ sàn/line vàng) để line vàng + sàn có thể tự giới hạn đúng trong viền
  // đỏ ngay từ đầu, khỏi bị lố ra ngoài ở 2 đầu — đây cũng là mốc cổng chính dùng bên dưới. Kéo
  // boundMinZ ra xa hơn (-90 thay vì -40) vì khu vực gần cổng chính (L2/R0) đang có người đứng lấn
  // lên ngay vạch đỏ, cần chừa thêm khoảng trống thật giữa cổng/viền và chỗ người đứng.
  var boundMinX = -(AISLE_HALF + RACK_SPAN) - 40, boundMaxX = (AISLE_HALF + RACK_SPAN) + 40;
  var boundMinZ = -90, boundMaxZ = totalLen + 40;

  // ---------- three.js scene (đồng bộ style ánh sáng/màu nền với warehouse-map.js) ----------
  var renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.outputColorSpace = THREE.SRGBColorSpace;

  var scene = new THREE.Scene();
  scene.background = new THREE.Color(0xd2d8df);
  scene.fog = new THREE.Fog(0xd2d8df, 1400, 3600);
  var camera = new THREE.PerspectiveCamera(42, 1, 1, 8000);

  scene.add(new THREE.HemisphereLight(0xffffff, 0x62696f, 1.0));
  var sun = new THREE.DirectionalLight(0xfff6e8, 1.2);
  sun.position.set(500, 800, 380); sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.camera.left = -900; sun.shadow.camera.right = 900;
  sun.shadow.camera.top = 900; sun.shadow.camera.bottom = -900;
  sun.shadow.camera.near = 50; sun.shadow.camera.far = 2400; sun.shadow.bias = -0.0015;
  scene.add(sun);
  var fill = new THREE.DirectionalLight(0xdbe6ff, 0.35); fill.position.set(-500, 400, -300); scene.add(fill);

  var postMat = new THREE.MeshStandardMaterial({ color: 0x2f74d0, metalness: 0.5, roughness: 0.4 });
  var braceMat = new THREE.MeshStandardMaterial({ color: 0x2662b4, metalness: 0.55, roughness: 0.45 });
  var beamMat = new THREE.MeshStandardMaterial({ color: 0xef8f22, metalness: 0.4, roughness: 0.5 });
  var deckMat = new THREE.MeshStandardMaterial({ color: 0xc7ccd2, metalness: 0.05, roughness: 0.9 });
  var footMat = new THREE.MeshStandardMaterial({ color: 0x2b2f36, metalness: 0.3, roughness: 0.7 });
  var palletMat = new THREE.MeshStandardMaterial({ color: 0xc08a45, roughness: 0.85, metalness: 0.02 });
  // Thùng carton thật (nâu, có băng keo + vệt loang) thay vì khối màu xanh trơn — vẽ canvas 1 lần,
  // dùng chung texture cho mọi mặt hộp (đủ giống thật ở khoảng cách nhìn bản đồ 3D, khỏi cần model
  // ngoài / ảnh riêng từng mặt như hộp anh gửi).
  function boxTexture() {
    var c = document.createElement('canvas'); c.width = c.height = 128; var x = c.getContext('2d');
    x.fillStyle = '#c9a06a'; x.fillRect(0, 0, 128, 128);
    x.fillStyle = 'rgba(120,80,40,0.18)';
    for (var i = 0; i < 26; i++) { x.fillRect(Math.random() * 128, Math.random() * 128, 14 + Math.random() * 16, 3); }
    x.fillStyle = '#e9dcbe'; x.fillRect(0, 50, 128, 22); // băng keo ngang giữa thùng
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
  }
  var crateMat = new THREE.MeshStandardMaterial({ map: boxTexture(), roughness: 0.85, metalness: 0.02 });
  var emptyCrateMat = new THREE.MeshStandardMaterial({ color: 0xaeb4bd, roughness: 0.8, metalness: 0.02, transparent: true, opacity: 0.35 });
  var lamMat = new THREE.MeshStandardMaterial({ color: 0xa8402b, metalness: 0.35, roughness: 0.5 });
  var tableMat = new THREE.MeshStandardMaterial({ color: 0x6b7280, metalness: 0.3, roughness: 0.55 });
  var monitorMat = new THREE.MeshStandardMaterial({ color: 0x1c2330, metalness: 0.4, roughness: 0.3 });
  var printerMat = new THREE.MeshStandardMaterial({ color: 0xe6e8eb, metalness: 0.1, roughness: 0.6 });

  function floorTex() {
    var c = document.createElement('canvas'); c.width = c.height = 512; var x = c.getContext('2d');
    x.fillStyle = '#bcc1c7'; x.fillRect(0, 0, 512, 512);
    x.strokeStyle = 'rgba(0,0,0,0.05)'; x.lineWidth = 2;
    for (var i = 0; i <= 512; i += 64) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i, 512); x.stroke(); x.beginPath(); x.moveTo(0, i); x.lineTo(512, i); x.stroke(); }
    var t = new THREE.CanvasTexture(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(Math.max(4, totalLen / 90), 10); return t;
  }
  var floor = new THREE.Mesh(new THREE.PlaneGeometry(1300, totalLen + 300), new THREE.MeshStandardMaterial({ map: floorTex(), roughness: 0.96, metalness: 0.02 }));
  floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, totalLen / 2); floor.receiveShadow = true; scene.add(floor);

  // Line vàng CHỈ dài đúng trong khoảng viền đỏ (boundMinZ..boundMaxZ) — trước đó dài hơn hẳn
  // (totalLen+300, tâm ở totalLen/2) nên lố ra NGOÀI viền đỏ ở cả 2 đầu, tạo phần vàng thừa đè lên
  // chỗ giao với line đỏ.
  var laneMat = new THREE.MeshStandardMaterial({ color: 0xf4c430, roughness: 0.8 });
  var lane = new THREE.Mesh(new THREE.PlaneGeometry(AISLE_HALF * 2 - 20, boundMaxZ - boundMinZ), laneMat);
  lane.rotation.x = -Math.PI / 2; lane.position.set(0, 0.4, (boundMinZ + boundMaxZ) / 2); lane.receiveShadow = true; scene.add(lane);

  // CHỈ vẽ dải lối đi ở những chỗ THẬT SỰ rộng (do GapBefore, vd trước L4 / trước 3 bàn) — khe hẹp
  // mặc định giữa 2 kệ liền kề (~6 đơn vị) không phải lối đi, để 2 kệ đọc liền thành 1 dải, chỉ có
  // 2 cột trụ sát nhau ("||") phân cách, đúng ý anh (không phải kệ nào cũng tách rời ra riêng).
  var smallWalkMat = new THREE.MeshStandardMaterial({ color: 0x9aa0a8, roughness: 0.85 });
  smallWalkways.forEach(function (w) {
    if (w.len <= 20) return;
    var strip = new THREE.Mesh(new THREE.PlaneGeometry(RACK_SPAN + 10, w.len), smallWalkMat);
    strip.rotation.x = -Math.PI / 2;
    var xOff = w.side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2 + 6) : (AISLE_HALF + RACK_SPAN / 2 + 6);
    strip.position.set(xOff, 0.35, w.z);
    strip.receiveShadow = true;
    scene.add(strip);
  });

  // Lối lấy hàng sát mặt trước MỖI kệ — chạy suốt chiều dài kệ (từ ô1 tới ô cuối), nối thẳng vào
  // lối đi lớn ở đầu ô1 (ô1 luôn ở gần lối đi lớn nên mép này tự chạm vào dải lối đi lớn, không
  // cần tính riêng điểm nối). Tô cùng màu lối đi lớn để đọc liền thành 1 hệ thống lối đi.
  // Giữ nguyên khoảng cách thật (PICK_AISLE_W) nhưng chỉ TÔ VÀNG 1 vạch mảnh ở giữa khoảng đó
  // (như vạch sơn sàn thật), không tô vàng hết cả bề rộng lối đi.
  var PICK_LINE_W = 16;
  var pickAisleMat = new THREE.MeshStandardMaterial({ color: 0x9aa0a8, roughness: 0.8 });
  ['L', 'R'].forEach(function (side) {
    placed[side].forEach(function (e) {
      var strip = new THREE.Mesh(new THREE.PlaneGeometry(RACK_SPAN + 6, PICK_LINE_W), pickAisleMat);
      strip.rotation.x = -Math.PI / 2;
      var xOff = side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2);
      strip.position.set(xOff, 0.38, e.z - e.thick / 2 - PICK_AISLE_W / 2);
      strip.receiveShadow = true;
      scene.add(strip);
    });
  });

  // Viền đỏ bao quanh mặt bằng (giống warehouse-map.js bên PMC) — mốc để anh xác định cửa ra vào
  // sau này, chưa gắn cửa thật, chỉ là đường viền tham chiếu.
  var boundMat = new THREE.MeshStandardMaterial({ color: 0xe0393f, roughness: 0.55, metalness: 0.1, emissive: 0x4a0d0d, emissiveIntensity: 0.35 });
  var boundThick = 10;
  function boundaryEdge(w, h, cx, cz) {
    var m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), boundMat);
    m.rotation.x = -Math.PI / 2; m.position.set(cx, 0.6, cz); m.receiveShadow = true; scene.add(m);
  }
  boundaryEdge(boundMaxX - boundMinX, boundThick, (boundMinX + boundMaxX) / 2, boundMinZ);
  boundaryEdge(boundMaxX - boundMinX, boundThick, (boundMinX + boundMaxX) / 2, boundMaxZ);
  boundaryEdge(boundThick, boundMaxZ - boundMinZ, boundMinX, (boundMinZ + boundMaxZ) / 2);
  boundaryEdge(boundThick, boundMaxZ - boundMinZ, boundMaxX, (boundMinZ + boundMaxZ) / 2);

  function roundRect(x, a, b, w, h, r) { x.beginPath(); x.moveTo(a + r, b); x.arcTo(a + w, b, a + w, b + h, r); x.arcTo(a + w, b + h, a, b + h, r); x.arcTo(a, b + h, a, b, r); x.arcTo(a, b, a + w, b, r); x.closePath(); }
  function makeSign(text, w) {
    var c = document.createElement('canvas'); c.width = 320; c.height = 96; var x = c.getContext('2d');
    x.fillStyle = 'rgba(20,58,110,0.94)'; roundRect(x, 4, 4, 312, 88, 14); x.fill();
    x.strokeStyle = 'rgba(255,255,255,0.25)'; x.lineWidth = 3; roundRect(x, 4, 4, 312, 88, 14); x.stroke();
    x.fillStyle = '#fff'; x.textAlign = 'center'; x.textBaseline = 'middle';
    // Tự co cỡ chữ cho vừa khung (thay vì cắt chữ "…") — chữ dài như "COMPONEMT INSPECTION" trước
    // đó bị tràn/khuất khỏi khung nền, giờ đo bề rộng thật rồi giảm cỡ chữ tới khi vừa 296px.
    var fontSize = 40;
    do {
      x.font = 'bold ' + fontSize + 'px "Arial Narrow", Arial, sans-serif';
      fontSize -= 2;
    } while (x.measureText(text).width > 296 && fontSize > 14);
    x.fillText(text, 160, 50);
    var tex = new THREE.CanvasTexture(c); tex.colorSpace = THREE.SRGBColorSpace;
    var s = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true })); s.scale.set(w || 46, (w || 46) * 0.3, 1);
    return s;
  }
  var numMatCache = {};
  function numMat(n) {
    if (numMatCache[n]) return numMatCache[n];
    var c = document.createElement('canvas'); c.width = c.height = 64; var x = c.getContext('2d');
    x.fillStyle = '#12325e'; x.beginPath(); x.arc(32, 32, 29, 0, Math.PI * 2); x.fill();
    x.strokeStyle = 'rgba(255,255,255,0.55)'; x.lineWidth = 3; x.beginPath(); x.arc(32, 32, 29, 0, Math.PI * 2); x.stroke();
    x.fillStyle = '#fff'; x.font = 'bold 34px Arial'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.fillText('T' + n, 32, 34);
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    numMatCache[n] = new THREE.MeshBasicMaterial({ map: t, transparent: true }); return numMatCache[n];
  }
  var numGeo = new THREE.PlaneGeometry(15, 15);

  // Nhãn mã vị trí gắn ngay trên thanh dầm của từng ô (anh khoanh đỏ chỗ này) — không cache theo
  // text vì mã gần như luôn khác nhau (876 vị trí thật), canvas cố tình nhỏ để nhẹ.
  function codeLabelTexture(text) {
    var c = document.createElement('canvas'); c.width = 160; c.height = 44; var x = c.getContext('2d');
    x.fillStyle = '#fff'; roundRect(x, 2, 2, 156, 40, 6); x.fill();
    x.strokeStyle = '#1c2330'; x.lineWidth = 3; roundRect(x, 2, 2, 156, 40, 6); x.stroke();
    x.fillStyle = '#1c2330'; x.font = 'bold 22px "Arial Narrow", Arial, sans-serif'; x.textAlign = 'center'; x.textBaseline = 'middle';
    x.fillText(text, 80, 23);
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
  }

  var _v1 = new THREE.Vector3(), _v2 = new THREE.Vector3();
  function connect(parent, ax, ay, az, bx, by, bz, thick, mat) {
    _v1.set(ax, ay, az); _v2.set(bx, by, bz); var len = _v1.distanceTo(_v2);
    var m = new THREE.Mesh(new THREE.BoxGeometry(thick, thick, len), mat);
    m.position.set((ax + bx) / 2, (ay + by) / 2, (az + bz) / 2); m.lookAt(_v2); m.castShadow = true; parent.add(m);
  }

  // ---------- công nhân chuyển động — dùng ĐÚNG cơ chế đi bộ của warehouse-map.js bên PMC (chân/tay
  // là PIVOT xoay ở hông/vai, công thức lắc chân "swing = sin(t*8)*0.5", tay vung ĐỐI chiều chân khi
  // đi tay không, giữ cố định khi ôm thùng/cầm scanner) thay vì chỉ trượt vị trí như bản trước — anh
  // chê "sao mấy con người không di chuyển" + "lấy model PMC xài đi, người ta đi tự nhiên lắm".
  var clock = new THREE.Clock();
  var walkers = [], idlers = [], typers = [], shuttles = [], conveyors = [], stairUnits = [], updateStairUnitsFn = null;
  // Băng chuyền — 1 nhóm vật (thùng carton) trượt đều theo 1 trục, hết "max" thì QUAY VÒNG về "min"
  // (không đảo chiều như walker/shuttle) — đúng kiểu hàng chạy trên băng chuyền thật.
  function addConveyor(items, axis, min, max, speed) {
    conveyors.push({ items: items, axis: axis, min: min, max: max, speed: speed });
  }
  // Đi qua LINE VÀNG LỚN (x=0) ở giữa thay vì cắt thẳng 1 đường giữa 2 điểm — cắt thẳng có thể XUYÊN
  // QUA bàn/kệ nằm giữa đường nếu 2 điểm không cùng 1 hàng thẳng trống trải (bug y hệt cầu thang lưu
  // động trước đó — xem buildApproachLegs trong buildRollingStairUnits, cùng nguyên lý).
  function buildLaneLegs(from, to, speed) {
    var pts = [from, { x: 0, z: from.z }, { x: 0, z: to.z }, to];
    var cleaned = [pts[0]];
    for (var i = 1; i < pts.length; i++) {
      var last = cleaned[cleaned.length - 1];
      if (Math.hypot(pts[i].x - last.x, pts[i].z - last.z) > 1) cleaned.push(pts[i]);
    }
    if (cleaned.length < 2) cleaned.push(to);
    var legs = [];
    for (var j = 0; j < cleaned.length - 1; j++) {
      var d = Math.hypot(cleaned[j + 1].x - cleaned[j].x, cleaned[j + 1].z - cleaned[j].z);
      legs.push({ from: cleaned[j], to: cleaned[j + 1], dur: Math.max(0.5, d / speed) });
    }
    return legs;
  }
  // Người đi con thoi (shuttle) giữa 2 điểm CỐ ĐỊNH qua line vàng lớn, ôm thùng cố định 2 tay — chỉ
  // hiện thùng lúc đang đi ĐẾN "to" (giao hàng), tay không lúc quay lại "from" (đi lấy đợt khác).
  function addShuttle(obj, carryBox, from, to, speed) {
    shuttles.push({
      obj: obj, carryBox: carryBox,
      legsForward: buildLaneLegs(from, to, speed), legsBack: buildLaneLegs(to, from, speed),
      forward: true, legIdx: 0, legT: 0,
    });
  }
  function addWalker(obj, axis, min, max, speed, mode, opts) {
    opts = opts || {};
    walkers.push({
      obj: obj, axis: axis, min: min, max: max, speed: speed, mode: mode || 'free',
      dir: opts.initialDir || (Math.random() < 0.5 ? 1 : -1),
      // carryBox + forwardDir: ẩn/hiện 1 mesh (thùng đang mang) theo CHIỀU đang đi — dùng cho người
      // lấy hàng thật từ 1 kệ đem qua chỗ khác (chỉ mang hàng lúc đi ĐÚNG chiều, tay không lúc quay về).
      carryBox: opts.carryBox || null, forwardDir: opts.forwardDir || 1,
    });
  }
  // Đứng yên tại chỗ nhưng ngó qua ngó lại như đang thao tác/kiểm tra — y hệt kiểu "onHold" PMC dùng
  // cho người ôm thùng đứng kiểm hàng (head.rotation.y = sin(hp*2π)*0.3).
  function addIdleChecker(headMesh, freq) {
    idlers.push({ head: headMesh, freq: freq, phase: Math.random() * Math.PI * 2 });
  }
  // Gõ phím + ngó màn hình — y hệt updateDeskPerson() bên PMC.
  function addTyper(handL, handR, head) {
    typers.push({ handL: handL, handR: handR, head: head, baseY: handL.position.y });
  }
  function updateAnimated(dt, t) {
    walkers.forEach(function (w) {
      var obj = w.obj, p = obj.position, u = obj.userData;
      p[w.axis] += w.dir * w.speed * dt;
      if (p[w.axis] > w.max) { p[w.axis] = w.max; w.dir = -1; obj.rotation.y += Math.PI; }
      else if (p[w.axis] < w.min) { p[w.axis] = w.min; w.dir = 1; obj.rotation.y += Math.PI; }
      var swing = Math.sin(t * 8) * 0.5;
      p.y = Math.abs(Math.sin(t * 8)) * 1.6;
      u.legL.rotation.x = swing; u.legR.rotation.x = -swing;
      if (w.mode === 'scanner') { u.armL.rotation.x = -swing * 0.8; u.armR.rotation.x = -1.55 + swing * 0.12; }
      else if (w.mode === 'carton') { u.armL.rotation.x = u.armR.rotation.x = -1.4; }
      else { u.armL.rotation.x = -swing * 0.7; u.armR.rotation.x = swing * 0.7; }
      if (w.carryBox) w.carryBox.visible = (w.dir === w.forwardDir);
    });
    shuttles.forEach(function (s) {
      s.legT += dt;
      var legs = s.forward ? s.legsForward : s.legsBack;
      var leg = legs[s.legIdx];
      var p = Math.min(1, s.legT / leg.dur);
      var u = s.obj.userData;
      s.obj.position.set(leg.from.x + (leg.to.x - leg.from.x) * p, Math.abs(Math.sin(t * 8)) * 1.6, leg.from.z + (leg.to.z - leg.from.z) * p);
      s.obj.rotation.y = Math.atan2(leg.to.x - leg.from.x, leg.to.z - leg.from.z);
      var swing = Math.sin(t * 8) * 0.5;
      u.legL.rotation.x = swing; u.legR.rotation.x = -swing;
      u.armL.rotation.x = u.armR.rotation.x = -1.4; // luôn ôm thùng 2 tay, không vung
      if (s.carryBox) s.carryBox.visible = s.forward;
      if (p >= 1) {
        s.legIdx++; s.legT = 0;
        if (s.legIdx >= legs.length) { s.forward = !s.forward; s.legIdx = 0; }
      }
    });
    conveyors.forEach(function (c) {
      var span = c.max - c.min;
      c.items.forEach(function (it) {
        it.position[c.axis] += c.speed * dt;
        if (it.position[c.axis] > c.max) it.position[c.axis] -= span;
      });
    });
    idlers.forEach(function (idl) { idl.head.rotation.y = Math.sin(t * idl.freq + idl.phase) * 0.3; });
    typers.forEach(function (ty) {
      ty.handL.position.y = ty.baseY + Math.max(0, Math.sin(t * 9)) * 0.9;
      ty.handR.position.y = ty.baseY + Math.max(0, Math.sin(t * 9 + Math.PI)) * 0.9;
      ty.head.rotation.y = Math.sin(t * 0.7) * 0.12;
      ty.head.rotation.x = Math.sin(t * 0.9) * 0.04 - 0.04;
    });
    if (updateStairUnitsFn) updateStairUnitsFn(dt, t);
  }

  function buildRack(entry, side) {
    var thick = entry.thick; // độ dày dọc Z (1 mặt/2 mặt)
    var levels = entry.levels;
    var levelH = (TOTAL_RACK_H - 14) / levels; // chiều cao 1 tầng RIÊNG của kệ này
    var totalH = levels * levelH + 14; // luôn = TOTAL_RACK_H
    var g = new THREE.Group();

    // dir: hướng từ mép kệ VỀ PHÍA lối đi lớn, tính theo trục X CỦA RIÊNG group này — dãy trái đặt
    // ở X âm nên "gần lối đi" là phía +X cục bộ; dãy phải đặt ở X dương nên "gần lối đi" là phía -X
    // cục bộ (xem cách tính xOff cuối hàm). Nhờ dir mà công thức cx bên dưới tự đối xứng đúng giữa
    // 2 dãy mà không cần đảo cellNo trong dữ liệu.
    var dir = side === 'L' ? 1 : -1;

    var postGeo = new THREE.BoxGeometry(POST, totalH, POST);
    var footGeo = new THREE.BoxGeometry(POST + 4, 4, POST + 4);
    [[-RACK_SPAN / 2, -thick / 2], [RACK_SPAN / 2, -thick / 2], [-RACK_SPAN / 2, thick / 2], [RACK_SPAN / 2, thick / 2]].forEach(function (p) {
      var post = new THREE.Mesh(postGeo, postMat); post.position.set(p[0], totalH / 2, p[1]); post.castShadow = true; g.add(post);
      var foot = new THREE.Mesh(footGeo, footMat); foot.position.set(p[0], 2, p[1]); g.add(foot);
    });
    // giằng chéo ở 2 đầu kệ (2 mép X, tức 2 "đầu hồi" của kệ) — song song trục Z (độ dày)
    [-RACK_SPAN / 2, RACK_SPAN / 2].forEach(function (sx) {
      for (var i = 0; i < levels; i++) {
        var y0 = i * levelH + BASE_Y, y1 = (i + 1) * levelH + BASE_Y;
        if (i % 2 === 0) connect(g, sx, y0, -thick / 2, sx, y1, thick / 2, 2.4, braceMat);
        else connect(g, sx, y0, thick / 2, sx, y1, -thick / 2, 2.4, braceMat);
      }
    });

    var beamGeo = new THREE.BoxGeometry(RACK_SPAN, BEAM_H, POST);
    var deckGeo = new THREE.BoxGeometry(RACK_SPAN - 4, 2, thick - 4);
    var cols = entry.cols;
    var cellLen = (RACK_SPAN - 10) / cols;

    var frontByLevel = byLevel(entry.rack.front);
    var backByLevel = byLevel(entry.rack.back);

    for (var lvl = 0; lvl < levels; lvl++) {
      var y = lvl * levelH + BASE_Y;
      [-thick / 2, thick / 2].forEach(function (dz) {
        var beam = new THREE.Mesh(beamGeo, beamMat); beam.position.set(0, y, dz); beam.castShadow = true; g.add(beam);
      });
      var deck = new THREE.Mesh(deckGeo, deckMat); deck.position.set(0, y + 1, 0); deck.receiveShadow = true; g.add(deck);

      // Mỗi ô có 1 trụ sắt mảnh riêng (chân đỡ, KHÔNG phải tấm chắn đặc) ở mép xa lối đi của ô đó,
      // chỉ cao đúng 1 tầng (từ sàn tầng này lên sàn tầng trên) — anh xác nhận: 1 tầng có N ô thì
      // có N trụ. Khác cột trụ chính (POST, cao suốt cả kệ) ở 2 đầu kệ.
      var miniPostGeo = new THREE.BoxGeometry(POST * 0.8, levelH, POST * 0.8);
      for (var oi = 0; oi < cols; oi++) {
        var px = dir * (RACK_SPAN / 2 - 5 - cellLen * (oi + 1));
        [-thick / 2, thick / 2].forEach(function (pz) {
          var miniPost = new THREE.Mesh(miniPostGeo, postMat);
          miniPost.position.set(px, y + levelH / 2, pz);
          miniPost.castShadow = true;
          g.add(miniPost);
        });
      }

      var num = new THREE.Mesh(numGeo, numMat(lvl + 1));
      num.position.set(-dir * (RACK_SPAN / 2 + 1), y + 16, -thick / 2 - 1);
      g.add(num);

      function placeCrates(cellsForLevel, faceSign) {
        (cellsForLevel || []).forEach(function (cell) {
          // Ô1 luôn ở đầu GẦN lối đi lớn nhất, ô lớn dần ra xa về phía vách (ảnh Excel anh gửi) —
          // "dir" tự lo việc đối xứng trái/phải, không cần đảo cellNo trong dữ liệu nữa.
          var idx = cell.cellNo - 1;
          var cx = dir * (RACK_SPAN / 2 - 5 - cellLen * idx - cellLen / 2);
          var cz = faceSign * (thick / 4);
          // Máy Lamination luôn hiện khối đặc (để biết đó là máy) dù ô chưa có mã vị trí thật.
          // Kệ chứa hàng thường: nếu đã nối được số lượng THẬT (qty, từ MES.MTL_BAR_BARCODE qua
          // I_AREA) thì hiện hàng theo đúng tồn kho thật (qty>0 mới hiện thùng) — mã có nhưng qty=0
          // nghĩa là vị trí đã đặt tên nhưng hiện KHÔNG có hàng thật, hiện ô trống (ghost). Nếu chưa
          // nối được qty (null, vd lỗi dblink) thì lùi về quy tắc cũ: có mã là hiện thùng.
          var hasRealQty = typeof cell.qty === 'number';
          var isFilled = entry.kind === 'lam' || (hasRealQty ? cell.qty > 0 : !!cell.code);
          if (isFilled) {
            var pallet = new THREE.Mesh(new THREE.BoxGeometry(cellLen * 0.82, PALLET_H, thick * 0.42), palletMat);
            pallet.position.set(cx, y + 2 + PALLET_H / 2, cz); pallet.castShadow = true; g.add(pallet);
            var mat = entry.kind === 'lam' ? lamMat : (entry.kind === 'table' ? tableMat : crateMat);
            // Ô quá dài (cellLen lớn) hoặc quá sâu (thick lớn) thì xếp NHIỀU thùng carton thay vì
            // chỉ 1 thùng — 1 thùng trông lọt thỏm/trống trải trong ô quá to.
            var boxCols = cellLen > 60 ? 3 : (cellLen > 32 ? 2 : 1);
            var boxRows = thick > 60 ? 2 : 1;
            var bw = (cellLen * 0.86) / boxCols, bd = (thick * 0.4) / boxRows;
            for (var br = 0; br < boxRows; br++) {
              for (var bc = 0; bc < boxCols; bc++) {
                var bx2 = cx - cellLen * 0.43 + bw * (bc + 0.5);
                var bz2 = cz - thick * 0.2 + bd * (br + 0.5);
                var crate = new THREE.Mesh(new THREE.BoxGeometry(bw * 0.86, CRATE_H, bd * 0.86), mat);
                crate.position.set(bx2, y + 2 + PALLET_H + CRATE_H / 2, bz2); crate.castShadow = true; g.add(crate);
              }
            }

            if (cell.code) {
              // Nhãn mã vị trí gắn trên thanh dầm của tầng này, sát mép trước/sau ứng với mặt của ô đó.
              // Cỡ CỐ ĐỊNH (không phóng to theo cellLen nữa — trước đó to dần khi kệ dài ra, quá to).
              var labelZ = faceSign <= 0 ? -thick / 2 : thick / 2;
              var LABEL_W = 12, LABEL_H = 12 * (44 / 160);
              var labelPlane = new THREE.Mesh(
                new THREE.PlaneGeometry(LABEL_W, LABEL_H),
                new THREE.MeshBasicMaterial({ map: codeLabelTexture(cell.code), transparent: true, side: THREE.DoubleSide })
              );
              // Đẩy nhãn ra NGOÀI hẳn mặt dầm (dầm dày POST đơn vị quanh tâm labelZ) — để 0.5 trước đó
              // làm nhãn nằm CHÌM TRONG khối dầm đặc, bị che khuất hoàn toàn, không thấy được. Đặt
              // Y = y (đúng tâm thanh dầm) chứ không phải phía trên dầm nữa — LABEL_H (12*44/160≈3.3)
              // đã nhỏ hơn hẳn BEAM_H (10) nên nằm gọn trong bề dày dầm, không tràn lên/xuống.
              var labelOut = POST / 2 + 3;
              labelPlane.position.set(cx, y, labelZ + (faceSign <= 0 ? -labelOut : labelOut));
              // PlaneGeometry mặc định quay mặt (chữ đọc đúng) về phía +Z — nhãn mặt trước nằm ở Z ÂM
              // nên phải xoay 180° mới quay đúng mặt ra ngoài (phía người đứng ở lối đi lớn nhìn vào);
              // nhãn mặt sau nằm ở Z DƯƠNG thì giữ nguyên. Bản trước xoay NGƯỢC lại nên chữ bị lật ngược.
              if (faceSign <= 0) labelPlane.rotation.y = Math.PI;
              g.add(labelPlane);
            }
          } else {
            // Ô trống: thay vì 1 tấm mỏng nhạt nhoà, xếp 2-3 thùng carton nhạt màu cho đỡ trống trải
            // (anh yêu cầu "cho nó đẹp") — vẫn mờ hơn thùng thật (opacity thấp) để phân biệt là ô
            // chưa có mã, không lẫn với hàng thật.
            var ghostN = cellLen > 26 ? 3 : 2;
            for (var gi = 0; gi < ghostN; gi++) {
              var gw = cellLen * 0.7 / ghostN;
              var ggx = cx - cellLen * 0.35 + gw * (gi + 0.5);
              var ghost = new THREE.Mesh(new THREE.BoxGeometry(gw * 0.8, CRATE_H * 0.6, thick * 0.3), emptyCrateMat);
              ghost.position.set(ggx, y + 2 + CRATE_H * 0.3, cz); g.add(ghost);
            }
          }
        });
      }
      placeCrates(frontByLevel[lvl + 1], entry.hasBack ? -1 : 0);
      if (entry.hasBack) placeCrates(backByLevel[lvl + 1], 1);
    }

    var sign = makeSign(entry.rack.label || entry.rack.rackId, 90);
    sign.position.set(0, totalH + 14, 0);
    g.add(sign);

    var xOff = side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2);
    g.position.set(xOff, 0, entry.z);
    scene.add(g);
  }

  // Bàn (Component Inspection / 3 cái bàn dài) KHÔNG phải kệ nhiều tầng — chỉ là 1 (hay N) cái bàn
  // dài đặt trên sàn, mỗi "ô" (cols) ứng với 1 cái bàn riêng đặt liền nhau theo chiều dài kệ.
  var TABLE_H = 34, TABLE_TOP_T = 4, TABLE_LEG_T = 7;
  function buildTable(entry, side) {
    var thick = entry.thick;
    var dir = side === 'L' ? 1 : -1;
    var cols = entry.cols;
    var segLen = (RACK_SPAN - 10) / cols;
    var g = new THREE.Group();

    for (var i = 0; i < cols; i++) {
      var cx = dir * (RACK_SPAN / 2 - 5 - segLen * i - segLen / 2);
      var top = new THREE.Mesh(new THREE.BoxGeometry(segLen * 0.88, TABLE_TOP_T, thick * 0.8), tableMat);
      top.position.set(cx, TABLE_H, 0); top.castShadow = true; top.receiveShadow = true; g.add(top);
      [[-1, -1], [1, -1], [-1, 1], [1, 1]].forEach(function (s) {
        var leg = new THREE.Mesh(new THREE.BoxGeometry(TABLE_LEG_T, TABLE_H, TABLE_LEG_T), footMat);
        leg.position.set(cx + s[0] * segLen * 0.38, TABLE_H / 2, s[1] * thick * 0.36);
        leg.castShadow = true; g.add(leg);
      });
    }

    var sign = makeSign(entry.rack.label || entry.rack.rackId, 90);
    sign.position.set(0, TABLE_H + 26, 0);
    g.add(sign);

    var xOff = side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2);
    g.position.set(xOff, 0, entry.z);
    scene.add(g);
  }

  // Label Printing Area — 1 bàn dài như buildTable, cộng thêm 3 máy tính (case + màn hình) + 2 máy in.
  function buildPrintArea(entry, side) {
    var thick = entry.thick;
    var dir = side === 'L' ? 1 : -1;
    var g = new THREE.Group();
    var deskLen = RACK_SPAN - 10;

    var top = new THREE.Mesh(new THREE.BoxGeometry(deskLen, TABLE_TOP_T, thick * 0.8), tableMat);
    top.position.set(0, TABLE_H, 0); top.castShadow = true; top.receiveShadow = true; g.add(top);
    [[-1, -1], [1, -1], [-1, 1], [1, 1]].forEach(function (s) {
      var leg = new THREE.Mesh(new THREE.BoxGeometry(TABLE_LEG_T, TABLE_H, TABLE_LEG_T), footMat);
      leg.position.set(s[0] * deskLen * 0.46, TABLE_H / 2, s[1] * thick * 0.36);
      leg.castShadow = true; g.add(leg);
    });

    for (var p = 0; p < 3; p++) {
      var px = dir * (deskLen / 2 - deskLen * (p + 0.5) / 3);
      var monitor = new THREE.Mesh(new THREE.BoxGeometry(26, 20, 4), monitorMat);
      monitor.position.set(px, TABLE_H + TABLE_TOP_T / 2 + 10, -thick * 0.18);
      monitor.castShadow = true; g.add(monitor);
      var pcCase = new THREE.Mesh(new THREE.BoxGeometry(8, 18, 16), monitorMat);
      pcCase.position.set(px, TABLE_H + TABLE_TOP_T / 2 + 9, thick * 0.22);
      pcCase.castShadow = true; g.add(pcCase);
    }
    for (var pr = 0; pr < 2; pr++) {
      var prx = dir * (deskLen / 2 - deskLen * (pr + 1) / 3);
      var printer = new THREE.Mesh(new THREE.BoxGeometry(20, 10, 16), printerMat);
      printer.position.set(prx, TABLE_H + TABLE_TOP_T / 2 + 5, thick * 0.05);
      printer.castShadow = true; g.add(printer);
    }

    var sign = makeSign(entry.rack.label || entry.rack.rackId, 90);
    sign.position.set(0, TABLE_H + 26, 0);
    g.add(sign);

    var xOff = side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2);
    g.position.set(xOff, 0, entry.z);
    scene.add(g);
  }

  function pickBuilder(kind) {
    if (kind === 'table') return buildTable;
    if (kind === 'print') return buildPrintArea;
    return buildRack;
  }
  placed.L.forEach(function (e) { pickBuilder(e.kind)(e, 'L'); });
  placed.R.forEach(function (e) { pickBuilder(e.kind)(e, 'R'); });

  // ---------- cổng ra vào (mốc tham chiếu, chưa phải cửa 3D chi tiết) ----------
  function findPlacedById(rackId) {
    return placed.L.concat(placed.R).find(function (e) { return e.rack.rackId === rackId; });
  }
  // Kiểu khung cổng giống hệt warehouse-map.js bên PMC: 2 cột vàng + xà ngang trên đầu, bảng tên
  // 2 DÒNG song ngữ Anh/Việt (PMC dùng I18N đổi ngôn ngữ theo nút bấm — bản generic này chưa nối
  // I18N nên in cứng cả 2 dòng luôn cho chắc ăn, khỏi phải bấm đổi).
  var gateMat = new THREE.MeshStandardMaterial({ color: 0xf1c21b, roughness: 0.6, metalness: 0.2 });
  var GATE_POST_H = 150, GATE_POST_T = 10;
  function makeGateSign(en, vi) {
    var c = document.createElement('canvas'); c.width = 320; c.height = 96; var x = c.getContext('2d');
    x.fillStyle = 'rgba(20,58,110,0.94)'; roundRect(x, 4, 4, 312, 88, 14); x.fill();
    x.strokeStyle = 'rgba(255,255,255,0.25)'; x.lineWidth = 3; roundRect(x, 4, 4, 312, 88, 14); x.stroke();
    x.fillStyle = '#fff'; x.textAlign = 'center';
    x.font = 'bold 30px "Arial Narrow", Arial, sans-serif'; x.textBaseline = 'alphabetic'; x.fillText(en, 160, 46);
    x.font = '600 22px "Arial Narrow", Arial, sans-serif'; x.fillText(vi, 160, 74);
    var tex = new THREE.CanvasTexture(c); tex.colorSpace = THREE.SRGBColorSpace;
    var s = new THREE.Sprite(new THREE.SpriteMaterial({ map: tex, transparent: true })); s.scale.set(140, 42, 1);
    return s;
  }
  function buildGate(x, z, axis, en, vi) {
    // axis 'x' = cổng nằm ngang trên cạnh Bắc/Nam (2 cột lệch theo X); axis 'z' = cổng nằm trên
    // cạnh Đông/Tây (2 cột lệch theo Z).
    [-70, 70].forEach(function (off) {
      var post = new THREE.Mesh(new THREE.BoxGeometry(GATE_POST_T, GATE_POST_H, GATE_POST_T), gateMat);
      post.position.set(axis === 'x' ? x + off : x, GATE_POST_H / 2, axis === 'x' ? z : z + off);
      post.castShadow = true; scene.add(post);
    });
    var header = new THREE.Mesh(
      new THREE.BoxGeometry(axis === 'x' ? 152 : GATE_POST_T, 12, axis === 'x' ? GATE_POST_T : 152),
      gateMat
    );
    header.position.set(x, GATE_POST_H, z);
    scene.add(header);
    var sign = makeGateSign(en, vi);
    sign.position.set(x, GATE_POST_H + 30, z);
    scene.add(sign);
  }
  // Cổng chính — sát COMPONENT INSPECTION (L2) và Label Printing Area (R0), đầu dãy (z≈0).
  buildGate(0, boundMinZ, 'x', 'MAIN GATE', 'CỔNG CHÍNH');
  // Cổng nhỏ vào khu văn phòng/vật tư — giữa L4 và L5 (chỗ khoảng trống lớn).
  var l4e = findPlacedById('L4'), l5e = findPlacedById('L5');
  if (l4e && l5e) {
    var lGateZ = (l4e.z + l4e.thick / 2 + l5e.z - l5e.thick / 2) / 2;
    buildGate(boundMinX, lGateZ, 'z', 'OFFICE MATERIAL GATE', 'CỔNG VẬT TƯ VĂN PHÒNG');
  }
  // Cổng nhỏ — sát Finish lamination (R22), đặt NGAY line vàng (lối đi lớn, x=0) thay vì mép tường.
  var r22e = findPlacedById('R22');
  if (r22e) {
    buildGate(0, boundMaxZ, 'x', 'SIDE GATE', 'CỔNG NHỎ');
  }

  // ---------- công nhân — MƯỢN NGUYÊN dáng người từ warehouse-map.js bên PMC (khối hộp/cầu, dáng
  // ngồi bàn PC + dáng đứng cầm scanner), chỉ đổi màu áo theo khu vực anh yêu cầu. ----------
  (function buildStaff() {
    var wSkinMat = new THREE.MeshStandardMaterial({ color: 0xd8a878, roughness: 0.8 });
    var wHairMat = new THREE.MeshStandardMaterial({ color: 0x1c1c1c, roughness: 0.6 });
    var wPantsSitMat = new THREE.MeshStandardMaterial({ color: 0x2b2f36, roughness: 0.8 });
    var wPantsStandMat = new THREE.MeshStandardMaterial({ color: 0x394452, roughness: 0.8 });
    var scanBodyMat = new THREE.MeshStandardMaterial({ color: 0x22262f, roughness: 0.35, metalness: 0.25 });
    var scanScreenMat = new THREE.MeshStandardMaterial({ color: 0x2fbf7a, roughness: 0.3, emissive: 0x2fbf7a, emissiveIntensity: 0.55 });
    var chairMat = new THREE.MeshStandardMaterial({ color: 0x24262c, roughness: 0.7, metalness: 0.1 });
    var chromeMat = new THREE.MeshStandardMaterial({ color: 0xb9c0c9, roughness: 0.3, metalness: 0.8 });

    // Dáng ngồi — y hệt buildOfficeDesk() bên PMC (hips/thighs/torso/head/tay áo ngắn + GHẾ xoay
    // chân chrome, trước đó port thiếu cái ghế), chỉ đổi màu áo (shirtColor) — mặc định 0x2389c9
    // PMC dùng đúng là "áo màu nước biển" anh yêu cầu.
    function buildSeated(shirtColor) {
      var shirtMat = new THREE.MeshStandardMaterial({ color: shirtColor, roughness: 0.7 });
      var g = new THREE.Group();
      var seat = new THREE.Mesh(new THREE.BoxGeometry(30, 4, 30), chairMat); seat.position.set(0, 27, 26); seat.castShadow = true; g.add(seat);
      var back = new THREE.Mesh(new THREE.BoxGeometry(28, 26, 4), chairMat); back.position.set(0, 42, 40); g.add(back);
      var chairPost = new THREE.Mesh(new THREE.CylinderGeometry(2, 2, 22, 10), chromeMat); chairPost.position.set(0, 15, 26); g.add(chairPost);
      var chairBase = new THREE.Mesh(new THREE.CylinderGeometry(13, 13, 1.6, 5), chromeMat); chairBase.position.set(0, 4, 26); g.add(chairBase);
      var hips = new THREE.Mesh(new THREE.BoxGeometry(18, 8, 14), wPantsSitMat); hips.position.set(0, 31, 28); hips.castShadow = true; g.add(hips);
      var thighs = new THREE.Mesh(new THREE.BoxGeometry(18, 7, 22), wPantsSitMat); thighs.position.set(0, 30, 10); thighs.castShadow = true; g.add(thighs);
      var torso = new THREE.Mesh(new THREE.BoxGeometry(19, 24, 7), shirtMat); torso.position.set(0, 47, 33.5); torso.castShadow = true; g.add(torso);
      var head = new THREE.Mesh(new THREE.SphereGeometry(7, 16, 12), wSkinMat); head.position.set(0, 66, 33.5); head.castShadow = true; g.add(head);
      var hair = new THREE.Mesh(new THREE.SphereGeometry(7.3, 16, 12, 0, Math.PI * 2, 0, Math.PI * 0.5), wHairMat); hair.position.set(0, 67.3, 33.5); g.add(hair);
      connect(g, -9.5, 57, 33.5, -8.87, 52.04, 23.21, 5, shirtMat);
      connect(g, -8.87, 52.04, 23.21, -8, 45.2, 9, 4.2, wSkinMat);
      connect(g, 9.5, 57, 33.5, 8.87, 52.04, 23.21, 5, shirtMat);
      connect(g, 8.87, 52.04, 23.21, 8, 45.2, 9, 4.2, wSkinMat);
      var handL = new THREE.Mesh(new THREE.BoxGeometry(5, 3, 6), wSkinMat); handL.position.set(-8, 45.2, 9); g.add(handL);
      var handR = new THREE.Mesh(new THREE.BoxGeometry(5, 3, 6), wSkinMat); handR.position.set(8, 45.2, 9); g.add(handR);
      g.userData.head = head; g.userData.handL = handL; g.userData.handR = handR;
      return g;
    }

    // Dáng đứng — y hệt buildWorker() bên PMC: chân/tay là PIVOT xoay ở hông/vai (không phải mesh
    // gắn cứng như bản trước) — bắt buộc phải là pivot mới lắc chân/vung tay khi đi được.
    function buildStanding(shirtColor, isFemale) {
      var vestMat = new THREE.MeshStandardMaterial({ color: shirtColor, roughness: 0.6 });
      var g = new THREE.Group(), legLen = isFemale ? 20 : 22, hipY = legLen;
      function leg(side) {
        var piv = new THREE.Group(); piv.position.set(side * 5, hipY, 0);
        var m = new THREE.Mesh(new THREE.BoxGeometry(6, legLen, 7), wPantsStandMat);
        m.position.set(0, -legLen / 2, 0); m.castShadow = true; piv.add(m);
        g.add(piv); return piv;
      }
      function arm(side) {
        var piv = new THREE.Group(); piv.position.set(side * (isFemale ? 8.5 : 10), hipY + 22, 0);
        var m = new THREE.Mesh(new THREE.BoxGeometry(5, 18, 6), vestMat); m.position.set(0, -9, 0); m.castShadow = true; piv.add(m);
        var hand = new THREE.Mesh(new THREE.BoxGeometry(4.5, 4, 5), wSkinMat); hand.position.set(0, -18, 0); piv.add(hand);
        g.add(piv); return piv;
      }
      var legL = leg(-1), legR = leg(1), armL = arm(-1), armR = arm(1);
      var hips = new THREE.Mesh(new THREE.BoxGeometry(isFemale ? 14 : 15, 7, 9), wPantsStandMat); hips.position.set(0, hipY + 2, 0); hips.castShadow = true; g.add(hips);
      var torso = new THREE.Mesh(new THREE.BoxGeometry(isFemale ? 14 : 16, 20, 9), vestMat); torso.position.set(0, hipY + 15, 0); torso.castShadow = true; g.add(torso);
      var head = new THREE.Mesh(new THREE.SphereGeometry(6, 14, 10), wSkinMat); head.position.set(0, hipY + 29, 0); head.castShadow = true; g.add(head);
      var hair = new THREE.Mesh(new THREE.SphereGeometry(6.2, 14, 10, 0, Math.PI * 2, 0, Math.PI * 0.85), wHairMat); hair.position.set(0, hipY + 30.3, 0); g.add(hair);
      g.userData.legL = legL; g.userData.legR = legR; g.userData.armL = armL; g.userData.armR = armR; g.userData.head = head;
      return g;
    }

    function buildScannerProp() {
      var g = new THREE.Group();
      var body = new THREE.Mesh(new THREE.BoxGeometry(5, 10, 2.6), scanBodyMat); g.add(body);
      var screen = new THREE.Mesh(new THREE.BoxGeometry(3.2, 4.6, 0.4), scanScreenMat); screen.position.set(0, 1.4, 1.5); g.add(screen);
      return g;
    }
    function buildCartonProp() {
      return new THREE.Mesh(new THREE.BoxGeometry(13, 11, 9), crateMat);
    }

    // 3 người ngồi Label Printing Area (R0) — áo tay ngắn màu xanh nước biển (0x2389c9 — đúng màu
    // PMC dùng sẵn), quay mặt vào bàn máy tính.
    var r0e = findPlacedById('R0');
    if (r0e) {
      var deskLenR0 = RACK_SPAN - 10;
      var r0xOff = AISLE_HALF + RACK_SPAN / 2; // R0 ở dãy phải
      for (var si = 0; si < 3; si++) {
        var sx = -1 * (deskLenR0 / 2 - deskLenR0 * (si + 0.5) / 3); // dir=-1 (dãy phải)
        var seated = buildSeated(0x2389c9);
        seated.position.set(r0xOff + sx, 0, r0e.z + r0e.thick * 0.35);
        scene.add(seated);
        addTyper(seated.userData.handL, seated.userData.handR, seated.userData.head); // gõ phím + ngó màn hình như PMC
      }
    }

    // Component Inspection (L2) — 4 người áo tím đứng thành 2 CẶP ĐỐI DIỆN nhau qua bàn (thao tác
    // như đang check hàng cùng nhau, không phải đứng thành 1 hàng ngang như trước), cộng thùng
    // carton nằm ngay trên mặt bàn (hàng đang chờ/được check), cộng 1 người áo tím thứ 5 đang bưng
    // carton tới đưa lên bàn cho 4 người kia check.
    var l2e = findPlacedById('L2');
    if (l2e) {
      var spanL2 = RACK_SPAN - 20;
      var purple = 0x7c4fd1;
      var l2xOff = -(AISLE_HALF + RACK_SPAN / 2);
      var frontZ = l2e.z - l2e.thick / 2 - 18;
      var backZ = l2e.z + l2e.thick / 2 + 18;
      [spanL2 * 0.28, -spanL2 * 0.28].forEach(function (ix, pi) {
        var front = buildStanding(purple, pi === 0);
        front.position.set(l2xOff + ix, 0, frontZ);
        scene.add(front);
        addIdleChecker(front.userData.head, 1.4 + pi * 0.3); // ngó qua ngó lại như đang check hàng (kiểu onHold PMC)
        var back = buildStanding(purple, pi !== 0);
        back.position.set(l2xOff + ix, 0, backZ);
        back.rotation.y = Math.PI;
        scene.add(back);
        addIdleChecker(back.userData.head, 1.6 + pi * 0.3);
      });

      // Bàn Component Inspection chạy kiểu BĂNG CHUYỀN — dải băng đen (footMat, cùng màu chân đế
      // các kệ) nổi nhẹ trên mặt bàn + thùng carton TRƯỢT ĐỀU dọc theo, hết đầu bên này lại quay
      // vòng về đầu kia (addConveyor), giống hàng thật đang chạy qua chỗ 4 người kiểm tra.
      var l2TopY = TABLE_H + TABLE_TOP_T / 2 + 11 / 2;
      var beltHalf = spanL2 * 0.4;
      var belt = new THREE.Mesh(new THREE.BoxGeometry(beltHalf * 2 + 14, 1.6, l2e.thick * 0.5), footMat);
      belt.position.set(l2xOff, TABLE_H + TABLE_TOP_T / 2 + 0.8, l2e.z);
      belt.receiveShadow = true;
      scene.add(belt);
      var l2Cartons = [-beltHalf * 0.66, 0, beltHalf * 0.66].map(function (cx) {
        var carton = buildCartonProp();
        carton.position.set(l2xOff + cx, l2TopY, l2e.z);
        carton.castShadow = true;
        scene.add(carton);
        return carton;
      });
      addConveyor(l2Cartons, 'x', l2xOff - beltHalf, l2xOff + beltHalf, 16);

      // Người thứ 5 — LẤY HÀNG THẬT từ kệ L1 (kệ có sẵn trong layout, ngay cạnh) rồi bưng qua bàn
      // Component Inspection (L2) cho 4 người kia check, đi hết lại quay về L1 lấy đợt khác. L1 có
      // Order LỚN HƠN L2 (đứng sau L2 trong dãy) nên nếu đi thẳng 1 đường dọc Z sẽ XUYÊN THẲNG qua
      // giữa cái bàn L2 (bàn nằm chắn ngay giữa đường) — phải đi vòng qua line vàng lớn (addShuttle)
      // y hệt cách đã sửa cho cầu thang lưu động.
      var l1e = findPlacedById('L1');
      if (l1e) {
        var l1FrontZ = l1e.z - l1e.thick / 2 - PICK_AISLE_W / 2;
        var carrier = buildStanding(purple, false);
        var carryBox = buildCartonProp();
        carryBox.position.set(0, 42, 16);
        carrier.add(carryBox);
        carrier.position.set(l2xOff, 0, l1FrontZ);
        scene.add(carrier);
        addShuttle(carrier, carryBox, { x: l2xOff, z: l1FrontZ }, { x: l2xOff, z: frontZ }, 60);
      }
    }

    // 7 người áo xanh lá đi theo LINE NHỎ (lối lấy hàng sát mặt trước từng kệ), KHÔNG đứng ở line
    // vàng lớn giữa lối đi chính nữa (line lớn dành để check kệ tổng quát) — 3 người cầm máy scan
    // (đi kiểm tra xung quanh), 4 người cầm thùng carton (đang xếp hàng lên kệ). Rải đều qua các kệ
    // thật (bỏ qua bàn/máy lamination) ở cả 2 dãy trái/phải.
    var green = 0x2fa84f;
    var rackEntries = placed.L.concat(placed.R).filter(function (e) { return e.kind === 'rack'; });
    rackEntries.sort(function (a, b) { return a.z - b.z; });
    var n = rackEntries.length;
    var greenSpots = [0.08, 0.2, 0.34, 0.48, 0.62, 0.76, 0.9];
    greenSpots.forEach(function (frac, gi) {
      if (!n) return;
      var e = rackEntries[Math.min(n - 1, Math.floor(frac * n))];
      var onLeft = placed.L.indexOf(e) !== -1;
      var xOff = onLeft ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2);
      var isScanner = gi < 3;
      var worker = buildStanding(green, gi % 2 === 1);
      var localX = (gi % 2 === 0 ? -1 : 1) * RACK_SPAN * 0.22;
      var wz = e.z - e.thick / 2 - PICK_AISLE_W / 2;
      worker.position.set(xOff + localX, 0, wz);
      worker.rotation.y = onLeft ? Math.PI * 0.5 : -Math.PI * 0.5;
      if (isScanner) {
        // Scanner gắn vào PIVOT tay phải (không phải mesh tay cứng) — tay phải gần như giữ nguyên
        // hướng scan, chỉ lắc nhẹ theo bước chân, y hệt onWalk() của người cầm scanner bên PMC.
        var scanner = buildScannerProp();
        scanner.position.set(0, -20, 3); scanner.rotation.x = -0.3;
        worker.userData.armR.add(scanner);
      } else {
        // Carton ôm bằng 2 tay, gắn cố định vào thân — 2 tay giữ hộp nên không vung tay khi đi.
        var carton = buildCartonProp();
        carton.position.set(0, 42, 16);
        worker.add(carton);
      }
      scene.add(worker);
      // Đi tới đi lui dọc chiều dài kệ (line nhỏ, trục X cục bộ) — kiểm tra/xếp hàng dọc cả dãy ô.
      addWalker(worker, 'x', xOff - RACK_SPAN * 0.42, xOff + RACK_SPAN * 0.42, 10 + (gi % 4) * 3, isScanner ? 'scanner' : 'carton');
    });

    // THÊM 2 công nhân áo xanh nữa đi tuần dọc LINE VÀNG LỚN (lối đi chính giữa 2 dãy) — khác với 7
    // người ở trên đứng dọc line nhỏ sát từng kệ; 2 người này đi suốt chiều dài kho trên chính lane
    // vàng, mỗi người 1 nửa bên (âm/dương X) để không đụng nhau.
    [-1, 1].forEach(function (side, gi2) {
      var walker2 = buildStanding(green, gi2 === 0);
      var gx2 = side * AISLE_HALF * 0.4;
      walker2.position.set(gx2, 0, totalLen * (gi2 === 0 ? 0.2 : 0.65));
      var scanner2 = buildScannerProp();
      scanner2.position.set(0, -20, 3); scanner2.rotation.x = -0.3;
      walker2.userData.armR.add(scanner2);
      scene.add(walker2);
      addWalker(walker2, 'z', 25, totalLen - 25, 17 + gi2 * 5, 'scanner');
    });

    // ---------- 2 cầu thang lưu động (giống ảnh anh gửi) — mỗi cái có 1 người ĐẨY tới kệ + 1 người
    // TRÈO lên check hàng (4 công nhân tổng cộng) — tự chọn kệ ngẫu nhiên mỗi vòng, đẩy sang kệ mới
    // sau khi check xong, lặp vô tận. Bậc thang leo lên theo +Z (đúng hướng mặt trước MỌI kệ, xem
    // ghi chú lối lấy hàng ở trên) nên cầu thang không cần xoay theo dãy trái/phải.
    (function buildRollingStairUnits() {
      var stairMetal = new THREE.MeshStandardMaterial({ color: 0x9aa0a8, roughness: 0.5, metalness: 0.45 });
      var stepMat = new THREE.MeshStandardMaterial({ color: 0x6b7178, roughness: 0.85 });
      var wheelMat = new THREE.MeshStandardMaterial({ color: 0x2b2f36, roughness: 0.6 });
      var hubMat = new THREE.MeshStandardMaterial({ color: 0xb23a2e, roughness: 0.4, metalness: 0.3 });
      var STAIR_STEPS = 5, STAIR_RISE = 22, STAIR_RUN = 11, STAIR_W = 44, STAIR_PLAT_D = 22;
      var STAIR_H = STAIR_STEPS * STAIR_RISE;

      function buildRollingStair() {
        var g = new THREE.Group();
        var zBack = -(STAIR_STEPS * STAIR_RUN) / 2; // mép xa kệ — chân cầu thang, bậc đầu tiên
        for (var i = 0; i < STAIR_STEPS; i++) {
          var y = STAIR_RISE * (i + 1), z = zBack + STAIR_RUN * (i + 0.5);
          var step = new THREE.Mesh(new THREE.BoxGeometry(STAIR_W, 3, STAIR_RUN * 0.94), stepMat);
          step.position.set(0, y, z); step.castShadow = true; step.receiveShadow = true; g.add(step);
        }
        var platZ = zBack + STAIR_STEPS * STAIR_RUN + STAIR_PLAT_D / 2 - STAIR_RUN * 0.06;
        var plat = new THREE.Mesh(new THREE.BoxGeometry(STAIR_W, 3, STAIR_PLAT_D), stepMat);
        plat.position.set(0, STAIR_H, platZ); plat.castShadow = true; plat.receiveShadow = true; g.add(plat);
        var platFrontZ = platZ + STAIR_PLAT_D / 2;
        [-1, 1].forEach(function (sx) {
          connect(g, sx * STAIR_W / 2, 2, zBack, sx * STAIR_W / 2, STAIR_H, platFrontZ, 4, stairMetal); // khung nghiêng đỡ bậc
          connect(g, sx * STAIR_W / 2, STAIR_H + 34, zBack, sx * STAIR_W / 2, STAIR_H + 34, platFrontZ, 3, stairMetal); // tay vịn nghiêng
          connect(g, sx * STAIR_W / 2, STAIR_H, platFrontZ, sx * STAIR_W / 2, STAIR_H + 34, platFrontZ, 3, stairMetal); // cột đỡ tay vịn đầu bệ
          connect(g, sx * STAIR_W / 2, STAIR_H, zBack, sx * STAIR_W / 2, STAIR_H + 34, zBack, 3, stairMetal); // cột đỡ tay vịn chân thang
        });
        connect(g, -STAIR_W / 2, STAIR_H + 34, platFrontZ, STAIR_W / 2, STAIR_H + 34, platFrontZ, 3, stairMetal); // tay vịn hậu (sát kệ)
        [[-1, zBack], [1, zBack], [-1, platFrontZ], [1, platFrontZ]].forEach(function (w) {
          var wheel = new THREE.Mesh(new THREE.CylinderGeometry(4.2, 4.2, 3, 12), wheelMat);
          wheel.rotation.z = Math.PI / 2; wheel.position.set(w[0] * STAIR_W / 2, 4, w[1]); wheel.castShadow = true; g.add(wheel);
          var hub = new THREE.Mesh(new THREE.SphereGeometry(1.6, 8, 8), hubMat); hub.position.set(w[0] * STAIR_W / 2, 4, w[1]); g.add(hub);
        });
        return { group: g, bottomZ: zBack - 12, topZ: platZ, topY: STAIR_H };
      }

      var stairRackPool = placed.L.concat(placed.R).filter(function (e) { return e.kind === 'rack'; });
      function pickStairTarget(pool) {
        if (!pool.length) return null;
        var e = pool[Math.floor(Math.random() * pool.length)];
        var onLeft = placed.L.indexOf(e) !== -1;
        return { x: onLeft ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2), z: e.z - e.thick / 2 - PICK_AISLE_W / 2 };
      }

      var STAIR_SPEED = 45; // anh chê "ở line vàng lớn đi nhanh quá" — giảm nửa tốc độ (90 -> 45)
      var PHASE_DUR = { climbUp: 1.8, check: 2.4, climbDown: 1.8, pause: 0.6 };
      // Đường đi BẮT BUỘC qua line vàng lớn (x=0) trước, chỉ rẽ vào line nhỏ (x của dãy đích) ở
      // NGAY sát điểm đến — trước đó đi 1 đường thẳng cắt chéo giữa 2 điểm bất kỳ nên có lúc CẮT
      // XUYÊN qua thân kệ ở giữa đường (kệ nào cũng nằm ở x=±(AISLE_HALF+RACK_SPAN/2), đi chéo qua
      // đúng dải x đó là đâm thẳng vào kệ). 3 chặng: rời line nhỏ hiện tại ra line vàng (đổi X, giữ
      // Z) → chạy dọc line vàng (đổi Z, giữ X=0) → rẽ vào line nhỏ đích (đổi X, giữ Z đích).
      function buildApproachLegs(from, to) {
        var pts = [
          { x: from.x, z: from.z },
          { x: 0, z: from.z },
          { x: 0, z: to.z },
          { x: to.x, z: to.z },
        ];
        var cleaned = [pts[0]];
        for (var i = 1; i < pts.length; i++) {
          var last = cleaned[cleaned.length - 1];
          if (Math.hypot(pts[i].x - last.x, pts[i].z - last.z) > 1) cleaned.push(pts[i]);
        }
        if (cleaned.length < 2) cleaned.push(to);
        var legs = [];
        for (var j = 0; j < cleaned.length - 1; j++) {
          var d = Math.hypot(cleaned[j + 1].x - cleaned[j].x, cleaned[j + 1].z - cleaned[j].z);
          // Trần cũ (4s/chặng) ép TỐC ĐỘ THẬT tăng vọt ở chặng đi dọc line vàng lớn (khoảng cách xa
          // hơn hẳn chặng rẽ vào/ra) — nhìn như đang "bay" qua. Nới trần lên 20s để tốc độ hiển thị
          // luôn gần đúng STAIR_SPEED thật, đi chậm lại đúng ý anh, chấp nhận chờ lâu hơn 1 chút.
          legs.push({ from: cleaned[j], to: cleaned[j + 1], dur: Math.min(20, Math.max(1, d / STAIR_SPEED)) });
        }
        return legs;
      }
      function buildStairUnit(pool, colorPusher, colorClimber) {
        var stair = buildRollingStair();
        scene.add(stair.group);
        var pusher = buildStanding(colorPusher, false), climber = buildStanding(colorClimber, true);
        scene.add(pusher); scene.add(climber);
        var start = pickStairTarget(pool) || { x: 0, z: 0 };
        stair.group.position.set(start.x, 0, start.z);
        pusher.position.set(start.x - 14, 0, start.z + stair.bottomZ - 10);
        climber.position.set(start.x + 14, 0, start.z + stair.bottomZ - 10);
        return { stair: stair, pusher: pusher, climber: climber, pool: pool, phase: 'pause', phaseT: 0, legs: null, legIdx: 0 };
      }
      // Dáng 2 người lúc đẩy — tay đưa ra nắm khung/tay vịn cầu thang (KHÔNG vung tay đi bộ bình
      // thường, vì cả 2 tay đều đang giữ cầu thang) — anh chỉ rõ "4 con cầm thang luôn".
      function setPushPose(w, swing) {
        w.userData.legL.rotation.x = swing; w.userData.legR.rotation.x = -swing;
        w.userData.armL.rotation.x = -1.15; w.userData.armR.rotation.x = -1.15;
      }
      function updateStairUnit(u, dt, t) {
        u.phaseT += dt;
        var pu = u.pusher, cl = u.climber, st = u.stair;
        if (u.phase === 'approach') {
          var leg = u.legs[u.legIdx];
          var p = Math.min(1, u.phaseT / leg.dur);
          var x = leg.from.x + (leg.to.x - leg.from.x) * p, z = leg.from.z + (leg.to.z - leg.from.z) * p;
          st.group.position.set(x, 0, z);
          var faceAngle = Math.atan2(leg.to.x - leg.from.x, leg.to.z - leg.from.z);
          var swing = Math.sin(t * 7) * 0.45;
          [pu, cl].forEach(function (w, wi) {
            w.position.set(st.group.position.x + (wi === 0 ? -14 : 14), Math.abs(Math.sin(t * 7)) * 1.4, st.group.position.z + st.bottomZ - 10);
            w.rotation.y = faceAngle;
            setPushPose(w, swing);
          });
          if (p >= 1) {
            u.legIdx++; u.phaseT = 0;
            if (u.legIdx >= u.legs.length) { u.phase = 'climbUp'; u.legIdx = 0; u.legs = null; }
          }
          return;
        }
        pu.position.set(st.group.position.x - 14, 0, st.group.position.z + st.bottomZ - 10);
        pu.rotation.y = 0; pu.userData.legL.rotation.x = pu.userData.legR.rotation.x = 0;
        pu.userData.armL.rotation.x = pu.userData.armR.rotation.x = -0.2;
        if (u.phase === 'climbUp' || u.phase === 'climbDown') {
          var down = u.phase === 'climbDown';
          var p2 = Math.min(1, u.phaseT / (down ? PHASE_DUR.climbDown : PHASE_DUR.climbUp));
          var pp = down ? 1 - p2 : p2;
          var climbSwing = Math.sin(t * 9) * 0.5;
          cl.position.set(st.group.position.x, pp * st.topY, st.group.position.z + st.bottomZ + (st.topZ - st.bottomZ) * pp);
          cl.rotation.y = 0; cl.userData.head.rotation.y = 0;
          cl.userData.legL.rotation.x = climbSwing; cl.userData.legR.rotation.x = -climbSwing;
          cl.userData.armL.rotation.x = -0.9 + climbSwing * 0.3; cl.userData.armR.rotation.x = -0.9 - climbSwing * 0.3;
          if (p2 >= 1) { u.phase = down ? 'pause' : 'check'; u.phaseT = 0; }
        } else if (u.phase === 'check') {
          cl.position.set(st.group.position.x, st.topY, st.group.position.z + st.topZ);
          cl.rotation.y = 0;
          cl.userData.legL.rotation.x = cl.userData.legR.rotation.x = 0;
          cl.userData.armL.rotation.x = cl.userData.armR.rotation.x = -0.9;
          cl.userData.head.rotation.y = Math.sin(t * 2.2) * 0.35;
          if (u.phaseT >= PHASE_DUR.check) { u.phase = 'climbDown'; u.phaseT = 0; }
        } else { // pause — nghỉ dưới đất chờ vòng kế tiếp (đứng cạnh pusher)
          cl.position.set(st.group.position.x + 14, 0, st.group.position.z + st.bottomZ - 10);
          cl.userData.legL.rotation.x = cl.userData.legR.rotation.x = 0;
          cl.userData.armL.rotation.x = cl.userData.armR.rotation.x = -0.2;
          if (u.phaseT >= PHASE_DUR.pause) {
            var from = { x: st.group.position.x, z: st.group.position.z };
            var to = pickStairTarget(u.pool) || from;
            u.legs = buildApproachLegs(from, to); u.legIdx = 0;
            u.phase = 'approach'; u.phaseT = 0;
          }
        }
      }

      var half = Math.ceil(stairRackPool.length / 2);
      var unitA = buildStairUnit(stairRackPool.slice(0, half), 0xd9832e, 0xd9832e);
      var unitB = buildStairUnit(stairRackPool.slice(half), 0x3f8fd9, 0x3f8fd9);
      stairUnits.push(unitA, unitB);
      updateStairUnitsFn = function (dt, t) { stairUnits.forEach(function (u) { updateStairUnit(u, dt, t); }); };
    })();
  })();

  // ---------- orbit camera ----------
  // Mặc định nhìn GẦN THẲNG TỪ TRÊN XUỐNG (phi nhỏ) và theta = PI/2 để trục dọc lối đi (Z, trục
  // dài, nhiều kệ) trải ra THEO CHIỀU NGANG màn hình — 2 dãy trái/phải thành 2 dải ngang song song
  // như bản vẽ 2D vẫn quen nhìn, không bị chéo/xa dần (anh yêu cầu "ngang hết, không có dọc").
  // Vẫn kéo chuột xoay được sang góc 3D bình thường nếu muốn.
  var DEFAULT_TARGET = new THREE.Vector3(0, 20, totalLen / 2);
  var DEFAULT_RADIUS = Math.max(900, totalLen * 0.85);
  var DEFAULT_THETA = Math.PI / 2, DEFAULT_PHI = 0.38;
  var target = DEFAULT_TARGET.clone();
  var radius = DEFAULT_RADIUS;
  var theta = DEFAULT_THETA, phi = DEFAULT_PHI;
  function updateCamera() {
    camera.position.x = target.x + radius * Math.sin(phi) * Math.sin(theta);
    camera.position.y = target.y + radius * Math.cos(phi);
    camera.position.z = target.z + radius * Math.sin(phi) * Math.cos(theta);
    camera.lookAt(target);
  }
  updateCamera();

  // ---------- nút Reset / Tự xoay (y hệt wh3d-btnReset/wh3d-btnSpin bên PMC) ----------
  var autoSpin = false;
  var btnReset = document.getElementById('whg-btn-reset');
  var btnSpin = document.getElementById('whg-btn-spin');
  function resetView() {
    autoSpin = false;
    target.copy(DEFAULT_TARGET); radius = DEFAULT_RADIUS; theta = DEFAULT_THETA; phi = DEFAULT_PHI;
    updateCamera();
    if (btnSpin) { btnSpin.classList.remove('on'); btnSpin.textContent = I18N.t('wgAutoSpinStart'); }
    if (window.WHG_closeLocationDrawer) window.WHG_closeLocationDrawer(); // giống PMC: Reset đóng luôn drawer chi tiết đang mở
  }
  if (btnReset) btnReset.addEventListener('click', resetView);
  if (btnSpin) {
    btnSpin.textContent = I18N.t('wgAutoSpinStart');
    btnSpin.addEventListener('click', function () {
      autoSpin = !autoSpin;
      // Bật tự xoay -> luôn bắt đầu lại từ góc nhìn toàn kho mặc định (giống PMC) — không xoay dở
      // dang từ chỗ đang zoom cận 1 ô (nhìn xoay ngay tại chỗ đó rất khó chịu/dễ va vào kệ).
      if (autoSpin) { target.copy(DEFAULT_TARGET); radius = DEFAULT_RADIUS; theta = DEFAULT_THETA; phi = DEFAULT_PHI; updateCamera(); }
      btnSpin.classList.toggle('on', autoSpin);
      btnSpin.textContent = autoSpin ? I18N.t('wgAutoSpinStop') : I18N.t('wgAutoSpinStart');
    });
    // Nút "Tự xoay" có 2 trạng thái chữ khác nhau (Bắt đầu/Dừng) — data-i18n trong cshtml chỉ biết 1
    // key cố định nên KHÔNG gắn cho nút này, tự cập nhật lại đúng trạng thái hiện tại ở đây khi đổi
    // ngôn ngữ (khác nút Reset — chỉ 1 trạng thái, cứ để data-i18n lo là đủ).
    I18N.onChange(function () {
      btnSpin.textContent = autoSpin ? I18N.t('wgAutoSpinStop') : I18N.t('wgAutoSpinStart');
    });
  }

  // Xoay tự do bình thường — chỉ chặn phi lật quá xuống dưới sàn / quá thẳng đứng lên trên, không
  // khoá hướng xoay ngang (theta) nữa.
  var PHI_MIN = 0.12, PHI_MAX = 1.5;
  var dragging = false, lastX = 0, lastY = 0;
  canvas.addEventListener('mousedown', function (e) { dragging = true; lastX = e.clientX; lastY = e.clientY; canvas.style.cursor = 'grabbing'; });
  window.addEventListener('mouseup', function () { dragging = false; canvas.style.cursor = 'grab'; });
  window.addEventListener('mousemove', function (e) {
    if (!dragging) return;
    theta -= (e.clientX - lastX) * 0.006;
    phi = Math.max(PHI_MIN, Math.min(PHI_MAX, phi - (e.clientY - lastY) * 0.006));
    lastX = e.clientX; lastY = e.clientY;
    updateCamera();
  });
  canvas.addEventListener('wheel', function (e) {
    e.preventDefault();
    radius = Math.max(150, Math.min(3600, radius + e.deltaY * 0.8));
    updateCamera();
  }, { passive: false });

  function resize() {
    var w = wrap.clientWidth, h = wrap.clientHeight;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  }
  window.addEventListener('resize', resize);
  resize();

  (function loop() {
    requestAnimationFrame(loop);
    var dt = Math.min(clock.getDelta(), 0.1);
    updateAnimated(dt, clock.elapsedTime);
    if (autoSpin) { theta += dt * 0.25; updateCamera(); }
    renderer.render(scene, camera);
  })();

  // ---------- tìm theo mã vị trí ----------
  var searchInput = document.getElementById('whg-search-input');
  var searchBtn = document.getElementById('whg-search-btn');
  var searchMsg = document.getElementById('whg-search-msg');

  function findByCode(code) {
    code = (code || '').trim();
    if (!code) return null;
    for (var i = 0; i < RACKS.length; i++) {
      var r = RACKS[i];
      var hitF = (r.front || []).find(function (c) { return c.code && c.code.toUpperCase() === code.toUpperCase(); });
      if (hitF) return { rack: r, code: hitF.code, levelNo: hitF.levelNo, cellNo: hitF.cellNo, face: 'front' };
      var hitB = (r.back || []).find(function (c) { return c.code && c.code.toUpperCase() === code.toUpperCase(); });
      if (hitB) return { rack: r, code: hitB.code, levelNo: hitB.levelNo, cellNo: hitB.cellNo, face: 'back' };
    }
    return null;
  }

  // Zoom CHÍNH XÁC vào đúng CHỖ TAG TRẮNG (mã vị trí) đang hiện trên dầm — không phải tâm pallet
  // chung chung. Tính lại Y ĐÚNG toạ độ tag (labelZ/labelOut) y hệt lúc dựng nhãn trong buildRack,
  // để camera nhìn thẳng vào đúng chỗ có tag, giống PMC tìm kệ là nhảy thẳng tới đúng kệ đó.
  function focusCell(res) {
    var entry = placed.L.concat(placed.R).find(function (e) { return e.rack.rackId === res.rack.rackId; });
    if (!entry) return false;
    var side = placed.L.indexOf(entry) !== -1 ? 'L' : 'R';
    var dir = side === 'L' ? 1 : -1;
    var xOff = side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2);
    var faceSign = entry.hasBack ? (res.face === 'back' ? 1 : -1) : -1;
    autoSpin = false; if (btnSpin) { btnSpin.classList.remove('on'); btnSpin.textContent = I18N.t('wgAutoSpinStart'); }
    // Zoom vào ĐÚNG Ô đang tìm (không phải tâm cả kệ chung chung nữa — bản "tâm cả kệ" trước đó làm
    // ô lệch tít 1 đầu (vd ô 7/7) rơi ra ngoài rìa khung hình, tag NỔI BẬT trong hình lại là 1 ô khác
    // ở giữa kệ, không phải ô anh vừa tìm — đúng cái "chưa oke" anh báo). Tính lại cx y hệt công thức
    // đặt ô lúc dựng kệ (buildRack -> placeCrates) để tâm khung hình rơi ĐÚNG vào ô đang tìm.
    var cellLen = (RACK_SPAN - 10) / entry.cols;
    var idx = res.cellNo - 1;
    var cx = dir * (RACK_SPAN / 2 - 5 - cellLen * idx - cellLen / 2);
    // Y phải tính ĐÚNG THEO TẦNG đang tìm (trước đó cố định 1 chiều cao giữa kệ nên tầng 1 — sát
    // đất — bị camera zoom gần mà chĩa lên giữa kệ, nhìn "không tới" đúng ô tầng 1 anh báo).
    var levelH = (TOTAL_RACK_H - 14) / entry.levels;
    var yTarget = (res.levelNo - 1) * levelH + BASE_Y + levelH / 2;
    var faceZ = faceSign * (entry.thick / 2 + 6);
    target.set(xOff + cx, yTarget, entry.z + faceZ);
    // QUAN TRỌNG: theta phải đổi theo ĐÚNG mặt (trước/sau) — trước đó theta không đổi nên dù target
    // đã đúng toạ độ mặt sau, camera vẫn đứng y nguyên phía mặt trước và bị chính hàng ô mặt trước
    // che khuất mất mặt sau (anh báo "NB118-71 ở mặt sau mà zoom vẫn ra mặt trước"). Camera giờ
    // luôn đứng về phía NGOÀI của đúng mặt cần xem (cos(theta) cùng dấu faceSign), đồng thời lệch
    // về phía lối đi lớn cho tự nhiên (sin(theta) cùng dấu "dir").
    //
    // MẶT SAU đứng trong hẻm NHỎ giữa kệ này và kệ kế tiếp (chỉ rộng đúng PICK_AISLE_W, ~98) — KHÔNG
    // rộng như lối đi lớn trước mặt trước (~AISLE_HALF*2). Trước đó dùng chung 1 công thức 45° cho cả
    // 2 mặt nên camera lùi ra sau theo Z tới ~127 đơn vị, VƯỢT QUA hẻm nhỏ đó và LỌT VÀO NGAY TRONG
    // THÂN kệ kế tiếp (nhìn thấy y hệt 1 mảng màu đặc — anh báo "ô tầng 1 zoom không tới" chính là
    // đây, xảy ra cho mọi tầng ở mặt sau chứ không riêng tầng 1, tầng 1 chỉ tình cờ anh thử trúng).
    // Mặt sau giờ lùi theo Z RẤT ÍT (chủ yếu dạt sang X) để chắc chắn nằm gọn trong hẻm nhỏ đó.
    var zDirSign = faceSign <= 0 ? -1 : 1;
    phi = 1.15; // gần ngang tầm mắt hơn (trước 0.95 còn hơi chúc xuống)
    radius = RACK_SPAN * 0.18; // sát hơn nữa (theo đúng góc anh tự chỉnh tay rồi lùi gần thêm)
    // Trực diện hơn — trước đó lệch chéo (mặt sau ép cứng dir*2 cho an toàn) nên mặt sau vẫn "xéo",
    // anh chê "còn xéo, muốn nhìn thẳng". Giờ tính THẲNG NHẤT CÓ THỂ dựa vào khoảng trống THẬT còn
    // lại tới kệ kế tiếp (thay vì áng chừng 1 tỉ lệ cố định) — hẻm rộng thì đứng thẳng luôn được,
    // hẻm hẹp thì mới phải nghiêng bớt sang X đúng bằng phần thiếu, không hơn không kém.
    var planarReach = radius * Math.sin(phi);
    if (faceSign > 0) {
      var sideList = side === 'L' ? placed.L : placed.R;
      var nextEntry = sideList[sideList.indexOf(entry) + 1];
      var avail = nextEntry ? (nextEntry.z - nextEntry.thick / 2) - (entry.z + faceZ) - 15 : planarReach;
      var zMag = Math.max(10, Math.min(planarReach, avail));
      var xMag = Math.sqrt(Math.max(0, planarReach * planarReach - zMag * zMag));
      theta = Math.atan2(dir * xMag, zDirSign * zMag);
    } else {
      theta = Math.atan2(dir * 0.5, zDirSign);
    }
    updateCamera();
    return true;
  }

  function runSearch() {
    var res = findByCode(searchInput.value);
    if (!res) { searchMsg.textContent = I18N.t('wgSearchLocNotFound'); return; }
    searchMsg.textContent = '';
    focusCell(res);
    // Ô show đúng ô đó — kéo luôn thông tin thật (all barcode/PO/style) của đúng vị trí này, kiểu
    // drawer chi tiết bên PMC — xem warehouse-dashboard-generic.js (WHG_openLocationDetail).
    if (window.WHG_openLocationDetail) window.WHG_openLocationDetail(res.code);
  }
  searchBtn.addEventListener('click', runSearch);
  searchInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') runSearch(); });
})();
