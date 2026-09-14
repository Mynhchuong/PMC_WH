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
  var RACK_SPAN = 78;       // mặt rộng 1 kệ (dọc trục X, chứa các ô) — cố định, khớp "chiều dài all
                             // kệ đều ngang nhau"
  var SEAM = 6;             // khe hẹp giữa 2 cột trụ của 2 kệ liền kề dọc Z ("||") — KHÔNG phải lối
                             // đi, chỉ đủ thấy 2 cột tách biệt, để nhiều kệ liền đọc thành 1 dải
  var GAP_UNIT = 110;       // 1 "đơn vị" GapBefore = 1 lối đi cỡ vừa (cộng thêm vào khoảng cách Z
                             // giữa 2 kệ) — xem 003_add_gap_before.sql
  var POST = 5, BEAM_H = 6, LEVEL_H = 46, BASE_Y = 10;
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
  var placed = { L: [], R: [] };
  ['L', 'R'].forEach(function (side) {
    var list = RACKS.filter(function (r) { return r.side === side; }).sort(function (a, b) { return a.order - b.order; });
    var z = 0, prevHalfThick = 0;
    list.forEach(function (r, idx) {
      var levels = Math.max(1, maxLevel(r.front), maxLevel(r.back));
      var cols = Math.max(1, maxCell(r.front), maxCell(r.back));
      var hasBack = (r.back || []).length > 0;
      var thick = thicknessOf(hasBack);
      if (idx > 0) z += prevHalfThick + SEAM + (r.gapBefore || 0) / 100 * GAP_UNIT + thick / 2;
      var isLam = /LAMINATION|FINISH/i.test(r.label || '');
      var isTable = /BÀNG|BANG|TABLE|INSPECTION/i.test(r.label || '');
      placed[side].push({
        rack: r, levels: levels, cols: cols, hasBack: hasBack, thick: thick,
        kind: isLam ? 'lam' : (isTable ? 'table' : 'rack'),
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
  var crateMat = new THREE.MeshStandardMaterial({ color: 0x2fbf7a, roughness: 0.6, metalness: 0.05 });
  var emptyCrateMat = new THREE.MeshStandardMaterial({ color: 0xaeb4bd, roughness: 0.8, metalness: 0.02, transparent: true, opacity: 0.35 });
  var lamMat = new THREE.MeshStandardMaterial({ color: 0xa8402b, metalness: 0.35, roughness: 0.5 });
  var tableMat = new THREE.MeshStandardMaterial({ color: 0x6b7280, metalness: 0.3, roughness: 0.55 });
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
  var smallWalkMat = new THREE.MeshStandardMaterial({ color: 0xe9d9a0, roughness: 0.85 });
  smallWalkways.forEach(function (w) {
    if (w.len <= 20) return;
    var strip = new THREE.Mesh(new THREE.PlaneGeometry(RACK_SPAN + 10, w.len), smallWalkMat);
    strip.rotation.x = -Math.PI / 2;
    var xOff = w.side === 'L' ? -(AISLE_HALF + RACK_SPAN / 2 + 6) : (AISLE_HALF + RACK_SPAN / 2 + 6);
    strip.position.set(xOff, 0.35, w.z);
    strip.receiveShadow = true;
    scene.add(strip);
  });

  function roundRect(x, a, b, w, h, r) { x.beginPath(); x.moveTo(a + r, b); x.arcTo(a + w, b, a + w, b + h, r); x.arcTo(a + w, b + h, a, b + h, r); x.arcTo(a, b + h, a, b, r); x.arcTo(a, b, a + w, b, r); x.closePath(); }
  function makeSign(text, w) {
    var c = document.createElement('canvas'); c.width = 320; c.height = 96; var x = c.getContext('2d');
    x.fillStyle = 'rgba(20,58,110,0.94)'; roundRect(x, 4, 4, 312, 88, 14); x.fill();
    x.strokeStyle = 'rgba(255,255,255,0.25)'; x.lineWidth = 3; roundRect(x, 4, 4, 312, 88, 14); x.stroke();
    x.fillStyle = '#fff'; x.font = 'bold 34px "Arial Narrow", Arial, sans-serif'; x.textAlign = 'center'; x.textBaseline = 'middle';
    var t = text.length > 16 ? text.slice(0, 15) + '…' : text;
    x.fillText(t, 160, 50);
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
    var totalH = levels * LEVEL_H + 14;
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
        var y0 = i * LEVEL_H + BASE_Y, y1 = (i + 1) * LEVEL_H + BASE_Y;
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
      var y = lvl * LEVEL_H + BASE_Y;
      [-thick / 2, thick / 2].forEach(function (dz) {
        var beam = new THREE.Mesh(beamGeo, beamMat); beam.position.set(0, y, dz); beam.castShadow = true; g.add(beam);
      });
      var deck = new THREE.Mesh(deckGeo, deckMat); deck.position.set(0, y + 1, 0); deck.receiveShadow = true; g.add(deck);

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
          if (cell.code) {
            var pallet = new THREE.Mesh(new THREE.BoxGeometry(cellLen * 0.82, PALLET_H, thick * 0.42), palletMat);
            pallet.position.set(cx, y + 2 + PALLET_H / 2, cz); pallet.castShadow = true; g.add(pallet);
            var mat = entry.kind === 'lam' ? lamMat : (entry.kind === 'table' ? tableMat : crateMat);
            var crate = new THREE.Mesh(new THREE.BoxGeometry(cellLen * 0.7, CRATE_H, thick * 0.36), mat);
            crate.position.set(cx, y + 2 + PALLET_H + CRATE_H / 2, cz); crate.castShadow = true; g.add(crate);
          } else {
            var ghost = new THREE.Mesh(new THREE.BoxGeometry(cellLen * 0.7, 3, thick * 0.3), emptyCrateMat);
            ghost.position.set(cx, y + 2.5, cz); g.add(ghost);
          }
        });
      }
      placeCrates(frontByLevel[lvl + 1], entry.hasBack ? -1 : 0);
      if (entry.hasBack) placeCrates(backByLevel[lvl + 1], 1);
    }

    var sign = makeSign(entry.rack.label || entry.rack.rackId, 44);
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

  placed.L.forEach(function (e) { buildRack(e, 'L'); });
  placed.R.forEach(function (e) { buildRack(e, 'R'); });

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
    target.set(xOff * 0.4, entry.levels * LEVEL_H * 0.5, entry.z);
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
