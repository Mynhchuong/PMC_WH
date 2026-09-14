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
  var PICK_AISLE_W = 65; // bề rộng lối đi sát mặt trước mỗi kệ, chạy từ ô1 tới ô cuối — 20 rồi 40
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

  var totalLen = Math.max(
    placed.L.length ? placed.L[placed.L.length - 1].z : 0,
    placed.R.length ? placed.R[placed.R.length - 1].z : 0
  ) + RACK_SPAN;

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
  var hitMat = new THREE.MeshBasicMaterial({ transparent: true, opacity: 0, depthWrite: false });

  function floorTex() {
    var c = document.createElement('canvas'); c.width = c.height = 512; var x = c.getContext('2d');
    x.fillStyle = '#bcc1c7'; x.fillRect(0, 0, 512, 512);
    x.strokeStyle = 'rgba(0,0,0,0.05)'; x.lineWidth = 2;
    for (var i = 0; i <= 512; i += 64) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i, 512); x.stroke(); x.beginPath(); x.moveTo(0, i); x.lineTo(512, i); x.stroke(); }
    var t = new THREE.CanvasTexture(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(Math.max(4, totalLen / 90), 10); return t;
  }
  var floor = new THREE.Mesh(new THREE.PlaneGeometry(1300, totalLen + 300), new THREE.MeshStandardMaterial({ map: floorTex(), roughness: 0.96, metalness: 0.02 }));
  floor.rotation.x = -Math.PI / 2; floor.position.set(0, 0, totalLen / 2 - RACK_SPAN / 2); floor.receiveShadow = true; scene.add(floor);

  var laneMat = new THREE.MeshStandardMaterial({ color: 0xf4c430, roughness: 0.8 });
  var lane = new THREE.Mesh(new THREE.PlaneGeometry(AISLE_HALF * 2 - 20, totalLen + 300), laneMat);
  lane.rotation.x = -Math.PI / 2; lane.position.set(0, 0.4, totalLen / 2 - RACK_SPAN / 2); lane.receiveShadow = true; scene.add(lane);

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
  var boundMinX = -(AISLE_HALF + RACK_SPAN) - 40, boundMaxX = (AISLE_HALF + RACK_SPAN) + 40;
  var boundMinZ = -40, boundMaxZ = totalLen - RACK_SPAN / 2 + 40;
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

  var interactive = [];

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
          // Máy Lamination luôn hiện khối đặc (để biết đó là máy) dù ô chưa có mã vị trí thật —
          // khác kệ chứa hàng thường, ô trống thì mới hiện khối mờ (ghost) như trước.
          var isFilled = !!cell.code || entry.kind === 'lam';
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

    var pad = new THREE.Mesh(new THREE.BoxGeometry(RACK_SPAN, totalH, thick), hitMat);
    pad.position.set(0, totalH / 2, 0);
    pad.userData.rackId = entry.rack.rackId;
    g.add(pad);
    interactive.push(pad);

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

    var pad = new THREE.Mesh(new THREE.BoxGeometry(RACK_SPAN, TABLE_H + 10, thick), hitMat);
    pad.position.set(0, (TABLE_H + 10) / 2, 0);
    pad.userData.rackId = entry.rack.rackId;
    g.add(pad);
    interactive.push(pad);

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

    var pad = new THREE.Mesh(new THREE.BoxGeometry(RACK_SPAN, TABLE_H + 30, thick), hitMat);
    pad.position.set(0, (TABLE_H + 30) / 2, 0);
    pad.userData.rackId = entry.rack.rackId;
    g.add(pad);
    interactive.push(pad);

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
    buildGate(boundMinX, lGateZ, 'z', 'SIDE GATE', 'CỔNG NHỎ');
  }
  // Cổng nhỏ — sát Finish lamination (R22), cuối dãy phải.
  var r22e = findPlacedById('R22');
  if (r22e) {
    buildGate(AISLE_HALF + RACK_SPAN / 2, boundMaxZ, 'x', 'SIDE GATE', 'CỔNG NHỎ');
  }

  // ---------- orbit camera ----------
  // Mặc định nhìn GẦN THẲNG TỪ TRÊN XUỐNG (phi nhỏ) và theta = PI/2 để trục dọc lối đi (Z, trục
  // dài, nhiều kệ) trải ra THEO CHIỀU NGANG màn hình — 2 dãy trái/phải thành 2 dải ngang song song
  // như bản vẽ 2D vẫn quen nhìn, không bị chéo/xa dần (anh yêu cầu "ngang hết, không có dọc").
  // Vẫn kéo chuột xoay được sang góc 3D bình thường nếu muốn.
  var target = new THREE.Vector3(0, 20, totalLen / 2 - RACK_SPAN / 2);
  var radius = Math.max(900, totalLen * 0.85);
  var theta = Math.PI / 2, phi = 0.38;
  function updateCamera() {
    camera.position.x = target.x + radius * Math.sin(phi) * Math.sin(theta);
    camera.position.y = target.y + radius * Math.cos(phi);
    camera.position.z = target.z + radius * Math.sin(phi) * Math.cos(theta);
    camera.lookAt(target);
  }
  updateCamera();

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

  var raycaster = new THREE.Raycaster();
  var mouse = new THREE.Vector2();
  canvas.addEventListener('click', function (e) {
    var r = canvas.getBoundingClientRect();
    mouse.x = ((e.clientX - r.left) / r.width) * 2 - 1;
    mouse.y = -((e.clientY - r.top) / r.height) * 2 + 1;
    raycaster.setFromCamera(mouse, camera);
    var hit = raycaster.intersectObjects(interactive, false);
    if (hit.length) showRackPanel(hit[0].object.userData.rackId);
  });

  function resize() {
    var w = wrap.clientWidth, h = wrap.clientHeight;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  }
  window.addEventListener('resize', resize);
  resize();

  (function loop() { requestAnimationFrame(loop); renderer.render(scene, camera); })();

  // ---------- panel chi tiết ----------
  var panel = document.getElementById('whg-panel');

  function chipRow(levelNo, cells, highlightCode) {
    var line = document.createElement('div');
    line.className = 'whg-level';
    var ln = document.createElement('span'); ln.className = 'ln'; ln.textContent = 'T' + levelNo;
    line.appendChild(ln);
    cells.forEach(function (c) {
      var chip = document.createElement('span');
      var isHl = highlightCode && c.code && c.code.toUpperCase() === highlightCode.toUpperCase();
      chip.className = 'whg-chip ' + (c.code ? 'filled' : 'empty') + (isHl ? ' hl' : '');
      chip.textContent = c.code || '·';
      line.appendChild(chip);
    });
    return line;
  }

  function showRackPanel(rackId, highlightCode) {
    var r = RACKS.find(function (x) { return x.rackId === rackId; });
    if (!r) return;
    panel.innerHTML = '';

    var rid = document.createElement('div'); rid.className = 'rid'; rid.textContent = r.rackId;
    var label = document.createElement('div'); label.className = 'label'; label.textContent = r.label || '(chưa đặt tên)';
    panel.appendChild(rid); panel.appendChild(label);
    if (r.note) {
      var note = document.createElement('div'); note.className = 'note'; note.textContent = r.note;
      panel.appendChild(note);
    }

    [['Mặt trước', r.front], ['Mặt sau', r.back]].forEach(function (pair) {
      if (!pair[1] || !pair[1].length) return;
      var grp = document.createElement('div'); grp.className = 'whg-facegrp';
      var ft = document.createElement('div'); ft.className = 'ft'; ft.textContent = pair[0];
      grp.appendChild(ft);
      var byLv = byLevel(pair[1]);
      Object.keys(byLv).map(Number).sort(function (a, b) { return a - b; }).forEach(function (lv) {
        grp.appendChild(chipRow(lv, byLv[lv], highlightCode));
      });
      panel.appendChild(grp);
    });
  }

  // ---------- tìm theo mã vị trí ----------
  var searchInput = document.getElementById('whg-search-input');
  var searchBtn = document.getElementById('whg-search-btn');
  var searchMsg = document.getElementById('whg-search-msg');

  function findByCode(code) {
    code = (code || '').trim();
    if (!code) return null;
    for (var i = 0; i < RACKS.length; i++) {
      var r = RACKS[i];
      var hit = (r.front || []).concat(r.back || []).find(function (c) {
        return c.code && c.code.toUpperCase() === code.toUpperCase();
      });
      if (hit) return { rack: r, code: hit.code };
    }
    return null;
  }

  function focusRack(rackId) {
    var entry = placed.L.concat(placed.R).find(function (e) { return e.rack.rackId === rackId; });
    if (!entry) return;
    var side = placed.L.indexOf(entry) !== -1 ? 'L' : 'R';
    var xOff = side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2) : (AISLE_HALF + RACK_SPAN / 2);
    target.set(xOff * 0.4, TOTAL_RACK_H * 0.5, entry.z);
    radius = 300;
    updateCamera();
  }

  function runSearch() {
    var res = findByCode(searchInput.value);
    if (!res) { searchMsg.textContent = 'Không thấy mã này.'; return; }
    searchMsg.textContent = '';
    focusRack(res.rack.rackId);
    showRackPanel(res.rack.rackId, res.code);
  }
  searchBtn.addEventListener('click', runSearch);
  searchInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') runSearch(); });
})();
