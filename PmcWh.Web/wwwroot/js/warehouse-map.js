// Bản đồ kho 3D — P5.1 (dữ liệu thật, tĩnh). Đọc window.PMC_TIERS do view tiêm vào,
// dựng kệ theo layout thật, tô màu trống/có hàng theo số mã QR mỗi ô.
// Tương tác click/tìm kiếm/bay-tới sẽ thêm ở P5.2.
(function () {
  if (typeof THREE === 'undefined') { console.error('THREE chưa nạp'); return; }
  var TIERS = window.PMC_TIERS || [];
  // I18N khai báo NGAY ĐẦU (không phải ở giữa file) vì các bảng hiệu 3D (CỬA RA VÀO, KỆ N,
  // THOÁT HIỂM, màn hình PC...) được dựng sớm lúc build scene và cần gọi I18N.t() ngay lúc đó.
  var I18N = window.PmcWhI18n || { t: function (k) { return k; }, onChange: function () {} };
  var i18nSignRefreshers = []; // các sprite/canvas có chữ cần vẽ lại khi đổi ngôn ngữ

  // ----- Gom dữ liệu theo kệ/tầng + suy ra số tầng mỗi kệ -----
  var LEVELS = {}, tierMap = {};
  TIERS.forEach(function (t) {
    if (!LEVELS[t.rackNo] || t.levelNo > LEVELS[t.rackNo]) LEVELS[t.rackNo] = t.levelNo;
    (tierMap[t.rackNo] = tierMap[t.rackNo] || {})[t.levelNo] = t;
  });

  var canvas = document.getElementById('wh3d-canvas');
  var wrap = document.getElementById('wh3d-wrap');
  var renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 2));
  renderer.shadowMap.enabled = true;
  renderer.shadowMap.type = THREE.PCFSoftShadowMap;
  renderer.outputColorSpace = THREE.SRGBColorSpace;

  var scene = new THREE.Scene();
  scene.background = new THREE.Color(0xd2d8df);
  scene.fog = new THREE.Fog(0xd2d8df, 2000, 4600);
  var camera = new THREE.PerspectiveCamera(42, 1, 1, 8000);

  scene.add(new THREE.HemisphereLight(0xffffff, 0x62696f, 1.0));
  var sun = new THREE.DirectionalLight(0xfff6e8, 1.2);
  sun.position.set(700, 1050, 480); sun.castShadow = true;
  sun.shadow.mapSize.set(2048, 2048);
  sun.shadow.camera.left = -1500; sun.shadow.camera.right = 1500;
  sun.shadow.camera.top = 1100; sun.shadow.camera.bottom = -1100;
  sun.shadow.camera.near = 100; sun.shadow.camera.far = 3200; sun.shadow.bias = -0.0015;
  scene.add(sun);
  var fill = new THREE.DirectionalLight(0xdbe6ff, 0.35); fill.position.set(-600, 500, -400); scene.add(fill);

  function floorTex() {
    var c = document.createElement('canvas'); c.width = c.height = 512; var x = c.getContext('2d');
    x.fillStyle = '#bcc1c7'; x.fillRect(0, 0, 512, 512);
    x.strokeStyle = 'rgba(0,0,0,0.05)'; x.lineWidth = 2;
    for (var i = 0; i <= 512; i += 64) { x.beginPath(); x.moveTo(i, 0); x.lineTo(i, 512); x.stroke(); x.beginPath(); x.moveTo(0, i); x.lineTo(512, i); x.stroke(); }
    var t = new THREE.CanvasTexture(c); t.wrapS = t.wrapT = THREE.RepeatWrapping; t.repeat.set(18, 18); return t;
  }
  var floor = new THREE.Mesh(new THREE.PlaneGeometry(6000, 3600), new THREE.MeshStandardMaterial({ map: floorTex(), roughness: 0.96, metalness: 0.02 }));
  floor.rotation.x = -Math.PI / 2; floor.receiveShadow = true; scene.add(floor);

  var postMat = new THREE.MeshStandardMaterial({ color: 0x2f74d0, metalness: 0.5, roughness: 0.4 });
  var braceMat = new THREE.MeshStandardMaterial({ color: 0x2662b4, metalness: 0.55, roughness: 0.45 });
  var beamMat = new THREE.MeshStandardMaterial({ color: 0xef8f22, metalness: 0.4, roughness: 0.5 });
  var deckMat = new THREE.MeshStandardMaterial({ color: 0xc7ccd2, metalness: 0.05, roughness: 0.9 });
  var footMat = new THREE.MeshStandardMaterial({ color: 0x2b2f36, metalness: 0.3, roughness: 0.7 });
  var palletMat = new THREE.MeshStandardMaterial({ color: 0xc08a45, roughness: 0.85, metalness: 0.02 });
  var stockMats = [new THREE.MeshStandardMaterial({ color: 0x2fbf7a, roughness: 0.6, metalness: 0.05 }),
                   new THREE.MeshStandardMaterial({ color: 0x35a86f, roughness: 0.6, metalness: 0.05 })];
  var hitMat = new THREE.MeshBasicMaterial({ transparent: true, opacity: 0, depthWrite: false });
  var stagingMat = new THREE.MeshStandardMaterial({ color: 0xcaa46a, roughness: 0.82, metalness: 0.03 });

  var labelTex = (function () {
    var c = document.createElement('canvas'); c.width = 128; c.height = 80; var x = c.getContext('2d');
    x.fillStyle = '#fff'; x.fillRect(0, 0, 128, 80);
    x.fillStyle = '#111'; for (var i = 8; i < 120; i += 4) { if (Math.random() > 0.4) x.fillRect(i, 10, 2, 42); }
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; return t;
  })();
  var labelMat = new THREE.MeshStandardMaterial({ map: labelTex, roughness: 0.9 });

  function roundRect(x, a, b, w, h, r) { x.beginPath(); x.moveTo(a + r, b); x.arcTo(a + w, b, a + w, b + h, r); x.arcTo(a + w, b + h, a, b + h, r); x.arcTo(a, b + h, a, b, r); x.arcTo(a, b, a + w, b, r); x.closePath(); }
  // text: chuỗi tĩnh, HOẶC 1 hàm () => chuỗi nếu chữ cần đổi theo ngôn ngữ (VD: 'KỆ 32' <-> 'RACK 32') —
  // truyền hàm thì sign tự đăng ký vào i18nSignRefreshers để vẽ lại canvas khi đổi ngôn ngữ.
  function makeSign(text, w, fs) {
    var c = document.createElement('canvas'); c.width = 320; c.height = 96; var x = c.getContext('2d');
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    function draw() {
      x.clearRect(0, 0, 320, 96);
      x.fillStyle = 'rgba(20,58,110,0.94)'; roundRect(x, 4, 4, 312, 88, 14); x.fill();
      x.strokeStyle = 'rgba(255,255,255,0.25)'; x.lineWidth = 3; roundRect(x, 4, 4, 312, 88, 14); x.stroke();
      x.fillStyle = '#fff'; x.font = 'bold ' + (fs || 46) + 'px "Arial Narrow", Arial, sans-serif'; x.textAlign = 'center'; x.textBaseline = 'middle';
      x.fillText(typeof text === 'function' ? text() : text, 160, 50);
      t.needsUpdate = true;
    }
    draw();
    if (typeof text === 'function') i18nSignRefreshers.push(draw);
    var s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, transparent: true })); s.scale.set(w || 46, (w || 46) * 0.3, 1); return s;
  }
  var _v1 = new THREE.Vector3(), _v2 = new THREE.Vector3();
  function connect(parent, ax, ay, az, bx, by, bz, thick, mat) {
    _v1.set(ax, ay, az); _v2.set(bx, by, bz); var len = _v1.distanceTo(_v2);
    var m = new THREE.Mesh(new THREE.BoxGeometry(thick, thick, len), mat);
    m.position.set((ax + bx) / 2, (ay + by) / 2, (az + bz) / 2); m.lookAt(_v2); m.castShadow = true; parent.add(m);
  }

  var RACK_W = 96, RACK_D = 42, POST = 5, BEAM_H = 6, LEVEL_H = 46, BASE_Y = 10;
  var CRATE_W = 74, CRATE_D = 30, CRATE_H = 28, PALLET_H = 5;
  var interactive = [], padByRackLevel = {}, tierObjs = {};
  var nTotal = 0, nOcc = 0, nQr = 0;
  var postGeo = {}, beamGeo = new THREE.BoxGeometry(RACK_W, BEAM_H, POST), deckGeo = new THREE.BoxGeometry(RACK_W - 4, 2, RACK_D - 4);
  var footGeo = new THREE.BoxGeometry(POST + 4, 4, POST + 4), palletGeo = new THREE.BoxGeometry(CRATE_W, PALLET_H, CRATE_D);
  var crateGeo = new THREE.BoxGeometry(CRATE_W - 6, CRATE_H, CRATE_D - 4), labelGeo = new THREE.PlaneGeometry(20, 13);
  var numGeo = new THREE.PlaneGeometry(13, 13), numMatCache = {};
  function numMat(n) {
    if (numMatCache[n]) return numMatCache[n];
    var c = document.createElement('canvas'); c.width = c.height = 64; var x = c.getContext('2d');
    x.fillStyle = '#12325e'; x.beginPath(); x.arc(32, 32, 29, 0, Math.PI * 2); x.fill();
    x.strokeStyle = 'rgba(255,255,255,0.55)'; x.lineWidth = 3; x.beginPath(); x.arc(32, 32, 29, 0, Math.PI * 2); x.stroke();
    x.fillStyle = '#fff'; x.font = 'bold 40px Arial'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.fillText(String(n), 32, 35);
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    numMatCache[n] = new THREE.MeshBasicMaterial({ map: t, transparent: true }); return numMatCache[n];
  }

  // Tạo pallet + thùng + tem cho 1 tầng có hàng (dùng cả lúc dựng lẫn lúc realtime bật hàng lên).
  function addCrateMeshes(obj) {
    var y = obj.y, g = obj.group;
    var pallet = new THREE.Mesh(palletGeo, palletMat); pallet.position.set(0, y + 2 + PALLET_H / 2, 0); pallet.castShadow = true; g.add(pallet);
    var crate = new THREE.Mesh(crateGeo, stockMats[obj.no % 2]); crate.position.set(0, y + 2 + PALLET_H + CRATE_H / 2, 0); crate.castShadow = true; g.add(crate);
    var lbl = new THREE.Mesh(labelGeo, labelMat); lbl.position.set(-CRATE_W / 4, y + 2 + PALLET_H + CRATE_H / 2, RACK_D / 2 - 6.5); g.add(lbl);
    obj.crates = [pallet, crate, lbl];
  }
  function removeCrateMeshes(obj) {
    obj.crates.forEach(function (m) { obj.group.remove(m); }); // geo/mat dùng chung, không dispose
    obj.crates = [];
  }

  function buildRack(no, cx, cz, face) {
    var levels = LEVELS[no] || 0; if (!levels) return;
    var g = new THREE.Group(); g.position.set(cx, 0, cz); g.rotation.y = face === 1 ? 0 : Math.PI;
    var totalH = levels * LEVEL_H + 14;
    if (!postGeo[levels]) postGeo[levels] = new THREE.BoxGeometry(POST, totalH, POST);
    [[-RACK_W / 2, -RACK_D / 2], [RACK_W / 2, -RACK_D / 2], [-RACK_W / 2, RACK_D / 2], [RACK_W / 2, RACK_D / 2]].forEach(function (p) {
      var post = new THREE.Mesh(postGeo[levels], postMat); post.position.set(p[0], totalH / 2, p[1]); post.castShadow = true; g.add(post);
      var foot = new THREE.Mesh(footGeo, footMat); foot.position.set(p[0], 2, p[1]); g.add(foot);
    });
    [-RACK_W / 2, RACK_W / 2].forEach(function (sx) {
      for (var i = 0; i < levels; i++) {
        var y0 = i * LEVEL_H + BASE_Y, y1 = (i + 1) * LEVEL_H + BASE_Y;
        if (i % 2 === 0) connect(g, sx, y0, -RACK_D / 2, sx, y1, RACK_D / 2, 2.4, braceMat);
        else connect(g, sx, y0, RACK_D / 2, sx, y1, -RACK_D / 2, 2.4, braceMat);
      }
    });
    for (var lvl = 0; lvl < levels; lvl++) {
      var y = lvl * LEVEL_H + BASE_Y;
      [-RACK_D / 2, RACK_D / 2].forEach(function (zz) { var beam = new THREE.Mesh(beamGeo, beamMat); beam.position.set(0, y, zz); beam.castShadow = true; g.add(beam); });
      var deck = new THREE.Mesh(deckGeo, deckMat); deck.position.set(0, y + 1, 0); deck.receiveShadow = true; g.add(deck);

      var tier = (tierMap[no] && tierMap[no][lvl + 1]) || null;
      var qrCount = tier ? tier.qrCount : 0;
      nTotal++; if (qrCount > 0) { nOcc++; nQr += qrCount; }

      var pad = new THREE.Mesh(new THREE.BoxGeometry(RACK_W, LEVEL_H * 0.92, RACK_D), hitMat);
      pad.position.set(0, y + LEVEL_H / 2, 0);
      pad.userData = { rack: no, level: lvl + 1, code: tier ? tier.code : (no + '.' + (lvl + 1)), qrCount: qrCount, locationId: tier ? tier.locationId : null };
      g.add(pad); interactive.push(pad);
      (padByRackLevel[no] = padByRackLevel[no] || {})[lvl + 1] = pad;

      // ref để realtime thêm/bỏ thùng động về sau
      var tobj = { group: g, y: y, no: no, level: lvl + 1, pad: pad, crates: [] };
      tierObjs[no + '.' + (lvl + 1)] = tobj;
      if (qrCount > 0) addCrateMeshes(tobj);

      var num = new THREE.Mesh(numGeo, numMat(lvl + 1)); num.position.set(-RACK_W / 2 + 7, y + 16, RACK_D / 2 + 2); g.add(num);
    }
    var sign = makeSign(function () { return I18N.t('rackWord') + no; }, 46); sign.position.set(0, totalH + 15, RACK_D / 2 + 5); g.add(sign);
    scene.add(g);
  }

  // ----- Layout: rack -> (x, z, face) — giống bản đồ thật (3 dãy, dãy giữa 2 hàng áp lưng) -----
  var PITCH = 120, CROSS_EXTRA = 120, L = 1240;
  function rowCenters(n, crossAfter) {
    var xs = [0], x = 0;
    for (var i = 1; i < n; i++) { x += PITCH + (i - 1 === crossAfter ? CROSS_EXTRA : 0); xs.push(x); }
    var span = xs[xs.length - 1] || 1, s = L / span; return xs.map(function (v) { return v * s; });
  }
  var Z1 = 0, ZA = 195, ZB = ZA + RACK_D, Z3 = ZB + RACK_D / 2 + 155 + RACK_D / 2;
  var shiftX = -L / 2, shiftZ = -(Z3 + RACK_D) / 2;
  var specs = [];
  var c1 = rowCenters(11, -1); for (var i = 0; i < 11; i++) specs.push([i + 1, c1[i], Z1, 1]);
  var cA = rowCenters(10, 7);
  for (i = 0; i < 8; i++) specs.push([12 + i, cA[i], ZA, -1]);
  specs.push([20, cA[8], ZA, -1]); specs.push([21, cA[9], ZA, -1]);
  for (i = 0; i < 8; i++) specs.push([22 + i, cA[i], ZB, 1]);
  specs.push([30, cA[8], ZB, 1]); specs.push([31, cA[9], ZB, 1]);
  var c3 = rowCenters(11, 9);
  for (i = 0; i < 10; i++) specs.push([32 + i, c3[i], Z3, -1]);
  specs.push([42, c3[10], Z3, -1]); specs.push([43, c3[10], Z3 + RACK_D, 1]);
  specs.forEach(function (s) { buildRack(s[0], s[1] + shiftX, s[2] + shiftZ, s[3]); });

  var laneMat = new THREE.MeshStandardMaterial({ color: 0xf4c430, roughness: 0.8 });
  [(Z1 + ZA) / 2, (ZB + Z3) / 2].forEach(function (zc) {
    var lane = new THREE.Mesh(new THREE.PlaneGeometry(L + 260, 9), laneMat); lane.rotation.x = -Math.PI / 2; lane.position.set(0, 0.4, zc + shiftZ); lane.receiveShadow = true; scene.add(lane);
  });

  // ----- Lối đi cắt ngang qua kệ (đúng chỗ có khoảng hở CROSS_EXTRA trong rowCenters) -----
  // Kệ 19|20 và kệ 29|30 dùng chung 1 x (cA[7]/cA[8]) vì dãy A và dãy B áp lưng nhau -> chỉ 1 lối
  // đi xuyên suốt cả 2 dãy. Kệ 41|42,43 chỉ xuyên qua bề dày riêng dãy 3.
  var crossAB_X = (cA[7] + cA[8]) / 2 + shiftX;
  var crossAB_Z = (ZA + ZB) / 2 + shiftZ;
  var crossAB_Len = (ZB - ZA) + RACK_D;
  var crossLaneAB = new THREE.Mesh(new THREE.PlaneGeometry(9, crossAB_Len), laneMat);
  crossLaneAB.rotation.x = -Math.PI / 2; crossLaneAB.position.set(crossAB_X, 0.4, crossAB_Z); crossLaneAB.receiveShadow = true; scene.add(crossLaneAB);

  var cross3_X = (c3[9] + c3[10]) / 2 + shiftX;
  var cross3_Z = Z3 + shiftZ;
  var cross3_Len = RACK_D + 80;
  var crossLane3 = new THREE.Mesh(new THREE.PlaneGeometry(9, cross3_Len), laneMat);
  crossLane3.rotation.x = -Math.PI / 2; crossLane3.position.set(cross3_X, 0.4, cross3_Z); crossLane3.receiveShadow = true; scene.add(crossLane3);

  // ----- Cửa ra vào + khu vực chờ (có hàng chờ) + thoát hiểm -----
  var xLeft = -L / 2 - 260;
  var zMidA = ((ZA + ZB) / 2) + shiftZ;
  function arrowPlane(w, h, dir) {
    var c = document.createElement('canvas'); c.width = 256; c.height = 256; var x = c.getContext('2d');
    x.fillStyle = 'rgba(47,191,122,0.85)'; x.beginPath(); x.moveTo(40, 90); x.lineTo(150, 90); x.lineTo(150, 55); x.lineTo(220, 128); x.lineTo(150, 200); x.lineTo(150, 166); x.lineTo(40, 166); x.closePath(); x.fill();
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    var m = new THREE.Mesh(new THREE.PlaneGeometry(w, h), new THREE.MeshStandardMaterial({ map: t, transparent: true })); m.rotation.x = -Math.PI / 2; m.rotation.z = dir; return m;
  }
  var gateMat = new THREE.MeshStandardMaterial({ color: 0xf1c21b, roughness: 0.6, metalness: 0.2 });
  [zMidA - 70, zMidA + 70].forEach(function (zz) { var p = new THREE.Mesh(new THREE.BoxGeometry(10, 150, 10), gateMat); p.position.set(xLeft, 75, zz); p.castShadow = true; scene.add(p); });
  var header = new THREE.Mesh(new THREE.BoxGeometry(10, 12, 152), gateMat); header.position.set(xLeft, 150, zMidA); scene.add(header);
  var doorSign = makeSign(function () { return I18N.t('doorSignText'); }, 150, 42); doorSign.scale.set(150, 40, 1); doorSign.position.set(xLeft, 180, zMidA); scene.add(doorSign);
  var entryArrow = arrowPlane(110, 110, 0); entryArrow.position.set(xLeft + 215, 0.5, zMidA); scene.add(entryArrow);

  /* === KHU VỰC CHỜ — ẩn tạm theo yêu cầu (tương lai cần thì bỏ comment để bật lại) ===
  var waitZone = new THREE.Mesh(new THREE.PlaneGeometry(150, 360), new THREE.MeshStandardMaterial({ color: 0x3f6fb0, transparent: true, opacity: 0.18, roughness: 1 })); waitZone.rotation.x = -Math.PI / 2; waitZone.position.set(xLeft + 105, 0.35, zMidA); scene.add(waitZone);
  var waitSign = (function () {
    var c = document.createElement('canvas'); c.width = 340; c.height = 80; var x = c.getContext('2d');
    x.fillStyle = 'rgba(47,90,158,0.92)'; roundRect(x, 3, 3, 334, 74, 12); x.fill(); x.fillStyle = '#fff'; x.font = 'bold 38px "Arial Narrow", Arial, sans-serif'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.fillText('KHU VỰC CHỜ', 170, 42);
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace; var s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, transparent: true })); s.scale.set(120, 28, 1); s.position.set(xLeft + 105, 50, zMidA); return s;
  })();
  scene.add(waitSign);
  [[xLeft + 72, zMidA - 95, 2], [xLeft + 138, zMidA - 95, 1], [xLeft + 72, zMidA + 5, 1], [xLeft + 138, zMidA + 5, 2], [xLeft + 72, zMidA + 105, 1], [xLeft + 138, zMidA + 105, 1]].forEach(function (s) {
    var px = s[0], pz = s[1];
    var pl = new THREE.Mesh(palletGeo, palletMat); pl.position.set(px, PALLET_H / 2, pz); pl.castShadow = true; scene.add(pl);
    var b1 = new THREE.Mesh(new THREE.BoxGeometry(CRATE_W, CRATE_H + 6, CRATE_D), stagingMat); b1.position.set(px, PALLET_H + (CRATE_H + 6) / 2, pz); b1.castShadow = true; scene.add(b1);
    if (s[2] === 2) { var b2 = new THREE.Mesh(new THREE.BoxGeometry(CRATE_W - 16, CRATE_H - 4, CRATE_D - 8), stagingMat); b2.position.set(px, PALLET_H + (CRATE_H + 6) + (CRATE_H - 4) / 2, pz); b2.castShadow = true; scene.add(b2); }
  });
  === hết KHU VỰC CHỜ === */
  // thoát hiểm giữa kệ 41 và 42/43
  var xEsc = (c3[9] + c3[10]) / 2 + shiftX, zEscOuter = Z3 + RACK_D + 75 + shiftZ;
  var escGreen = new THREE.MeshStandardMaterial({ color: 0x2e9e5b, roughness: 0.6, metalness: 0.2 });
  [xEsc - 34, xEsc + 34].forEach(function (xx) { var pl = new THREE.Mesh(new THREE.BoxGeometry(9, 150, 9), escGreen); pl.position.set(xx, 75, zEscOuter); pl.castShadow = true; scene.add(pl); });
  var escHeader = new THREE.Mesh(new THREE.BoxGeometry(80, 12, 9), escGreen); escHeader.position.set(xEsc, 150, zEscOuter); scene.add(escHeader);
  var escSign = (function () {
    var c = document.createElement('canvas'); c.width = 300; c.height = 96; var x = c.getContext('2d');
    var t = new THREE.CanvasTexture(c); t.colorSpace = THREE.SRGBColorSpace;
    function draw() {
      x.clearRect(0, 0, 300, 96);
      x.fillStyle = '#1f8a4c'; roundRect(x, 4, 4, 292, 88, 12); x.fill(); x.fillStyle = '#fff'; x.font = 'bold 40px "Arial Narrow", Arial, sans-serif'; x.textAlign = 'center'; x.textBaseline = 'middle'; x.fillText(I18N.t('exitSignText'), 150, 50);
      t.needsUpdate = true;
    }
    draw(); i18nSignRefreshers.push(draw);
    var s = new THREE.Sprite(new THREE.SpriteMaterial({ map: t, transparent: true })); s.scale.set(100, 32, 1); s.position.set(xEsc, 172, zEscOuter); return s;
  })();
  scene.add(escSign);

  // ----- Bàn làm việc + PC trước kệ 32 (chỗ nhân viên ngồi quản lý khu vực) -----
  (function buildOfficeDesk() {
    var deskWoodMat = new THREE.MeshStandardMaterial({ color: 0x8a5a34, roughness: 0.75, metalness: 0.05 });
    var deskTopMat = new THREE.MeshStandardMaterial({ color: 0xc59a63, roughness: 0.5, metalness: 0.05 });
    var pcMat = new THREE.MeshStandardMaterial({ color: 0x2b2f36, roughness: 0.5, metalness: 0.3 });
    var chairMat = new THREE.MeshStandardMaterial({ color: 0x24262c, roughness: 0.7, metalness: 0.1 });
    var chromeMat = new THREE.MeshStandardMaterial({ color: 0xb9c0c9, roughness: 0.3, metalness: 0.8 });

    // Màn hình PC: vẽ giao diện giám sát đơn giản lên texture cho giống thật, không phải màn hình trơn
    var sc = document.createElement('canvas'); sc.width = 256; sc.height = 160; var sx = sc.getContext('2d');
    var screenTex = new THREE.CanvasTexture(sc); screenTex.colorSpace = THREE.SRGBColorSpace;
    function drawScreen() {
      sx.clearRect(0, 0, 256, 160);
      sx.fillStyle = '#0c2033'; sx.fillRect(0, 0, 256, 160);
      sx.strokeStyle = '#2fbf7a'; sx.lineWidth = 3; sx.beginPath();
      sx.moveTo(10, 120); sx.lineTo(60, 92); sx.lineTo(100, 106); sx.lineTo(150, 60); sx.lineTo(200, 78); sx.lineTo(246, 42); sx.stroke();
      sx.fillStyle = '#e9edf5'; sx.font = 'bold 20px Arial'; sx.fillText(I18N.t('deskScreenTitle'), 14, 32);
      sx.fillStyle = '#8a93a6'; sx.font = '13px Arial'; sx.fillText(I18N.t('deskScreenSub'), 14, 52);
      screenTex.needsUpdate = true;
    }
    drawScreen(); i18nSignRefreshers.push(drawScreen);
    var screenMat = new THREE.MeshStandardMaterial({ map: screenTex, roughness: 0.4, emissive: 0xffffff, emissiveMap: screenTex, emissiveIntensity: 0.5 });

    // Cùng trục (cùng Z) với dãy 3, nằm TRƯỚC kệ 32 (nhỏ hơn X của kệ 32 1 pitch) — chỗ trống trên
    // đường đi từ cửa vào đến kệ 32, là 1 khu riêng chứ không nằm trong khe hở giữa dãy B và dãy 3.
    var deskX = c3[0] + shiftX - 130;
    var deskZ = Z3 + shiftZ;
    var desk = new THREE.Group(); desk.position.set(deskX, 0, deskZ);

    var top = new THREE.Mesh(new THREE.BoxGeometry(112, 3, 58), deskTopMat); top.position.set(0, 44, 0); top.castShadow = true; top.receiveShadow = true; desk.add(top);
    [[-52, -25], [52, -25], [-52, 25], [52, 25]].forEach(function (p) {
      var leg = new THREE.Mesh(new THREE.BoxGeometry(4, 44, 4), deskWoodMat); leg.position.set(p[0], 22, p[1]); leg.castShadow = true; desk.add(leg);
    });
    var panel = new THREE.Mesh(new THREE.BoxGeometry(106, 26, 2), deskWoodMat); panel.position.set(0, 26, -22); desk.add(panel);

    var cpu = new THREE.Mesh(new THREE.BoxGeometry(16, 38, 38), pcMat); cpu.position.set(42, 19, -8); cpu.castShadow = true; desk.add(cpu);

    var standNeck = new THREE.Mesh(new THREE.CylinderGeometry(1.6, 1.6, 14, 10), chromeMat); standNeck.position.set(-10, 52, -16); desk.add(standNeck);
    var standBase = new THREE.Mesh(new THREE.CylinderGeometry(9, 9, 1.6, 20), chromeMat); standBase.position.set(-10, 45.8, -16); desk.add(standBase);
    var monitor = new THREE.Mesh(new THREE.BoxGeometry(38, 24, 2), screenMat); monitor.position.set(-10, 66, -16); monitor.castShadow = true; desk.add(monitor);

    var keyboard = new THREE.Mesh(new THREE.BoxGeometry(24, 1.4, 9), pcMat); keyboard.position.set(-10, 45.7, 10); desk.add(keyboard);
    var mouse = new THREE.Mesh(new THREE.BoxGeometry(4, 1.4, 6), pcMat); mouse.position.set(8, 45.7, 10); desk.add(mouse);

    var seat = new THREE.Mesh(new THREE.BoxGeometry(30, 4, 30), chairMat); seat.position.set(0, 27, 26); seat.castShadow = true; desk.add(seat);
    var back = new THREE.Mesh(new THREE.BoxGeometry(28, 26, 4), chairMat); back.position.set(0, 42, 40); desk.add(back);
    var chairPost = new THREE.Mesh(new THREE.CylinderGeometry(2, 2, 22, 10), chromeMat); chairPost.position.set(0, 15, 26); desk.add(chairPost);
    var chairBase = new THREE.Mesh(new THREE.CylinderGeometry(13, 13, 1.6, 5), chromeMat); chairBase.position.set(0, 4, 26); desk.add(chairBase);

    scene.add(desk);
  })();

  // ----- HUD stats -----
  function setTxt(id, v) { var el = document.getElementById(id); if (el) el.textContent = v; }
  setTxt('wh3d-stat-total', nTotal); setTxt('wh3d-stat-occ', nOcc); setTxt('wh3d-stat-qr', nQr);
  var loading = document.getElementById('wh3d-loading'); if (loading) loading.style.display = 'none';

  // ----- Khung highlight tầng đang chọn -----
  var hlBox = new THREE.LineSegments(new THREE.EdgesGeometry(new THREE.BoxGeometry(RACK_W + 8, LEVEL_H - 2, RACK_D + 8)), new THREE.LineBasicMaterial({ color: 0xffc400 }));
  hlBox.visible = false; scene.add(hlBox);
  var _hp = new THREE.Vector3();
  function showHighlight(pad) { pad.updateWorldMatrix(true, false); pad.getWorldPosition(_hp); hlBox.position.copy(_hp); hlBox.visible = true; }

  // ----- Orbit + fly-to + hover -----
  var target = new THREE.Vector3(0, 70, 0);
  var radius = 1550, theta = -0.7, phi = 1.0, autoSpin = false;
  var animCamPos = null, animTarget = null;
  var reduceMotion = window.matchMedia('(prefers-reduced-motion: reduce)').matches;
  function updateCamera() {
    camera.position.x = target.x + radius * Math.sin(phi) * Math.sin(theta);
    camera.position.y = target.y + radius * Math.cos(phi);
    camera.position.z = target.z + radius * Math.sin(phi) * Math.cos(theta);
    camera.lookAt(target);
  }
  function syncOrbit() { var o = camera.position.clone().sub(target); radius = o.length(); phi = Math.acos(Math.max(-1, Math.min(1, o.y / radius))); theta = Math.atan2(o.x, o.z); }
  var UP = new THREE.Vector3(0, 1, 0), _q = new THREE.Quaternion(), _fr = new THREE.Vector3(), _sd = new THREE.Vector3();
  // LUÔN đứng ở mặt trước kệ (bên có tem + số tầng) rồi zoom sát.
  function focusPad(pad) {
    pad.updateWorldMatrix(true, false);
    animTarget = pad.getWorldPosition(new THREE.Vector3());
    pad.getWorldQuaternion(_q); _fr.set(0, 0, 1).applyQuaternion(_q); _fr.y = 0; _fr.normalize();
    _sd.copy(UP).cross(_fr).normalize();
    animCamPos = animTarget.clone().addScaledVector(_fr, 150).addScaledVector(_sd, 52).addScaledVector(UP, 96);
  }
  function resize() { var w = wrap.clientWidth, h = wrap.clientHeight; renderer.setSize(w, h, false); camera.aspect = w / h; camera.updateProjectionMatrix(); }
  window.addEventListener('resize', resize);

  var dragging = false, lastX = 0, lastY = 0, moved = 0;
  canvas.addEventListener('pointerdown', function (e) { dragging = true; lastX = e.clientX; lastY = e.clientY; moved = 0; if (animCamPos) { animCamPos = null; syncOrbit(); } canvas.setPointerCapture(e.pointerId); canvas.style.cursor = 'grabbing'; });
  canvas.addEventListener('pointermove', function (e) {
    if (!dragging) { hover(e); return; }
    var dx = e.clientX - lastX, dy = e.clientY - lastY; lastX = e.clientX; lastY = e.clientY; moved += Math.abs(dx) + Math.abs(dy);
    theta -= dx * 0.006; phi = Math.max(0.28, Math.min(1.5, phi - dy * 0.005)); updateCamera();
  });
  ['pointerup', 'pointercancel'].forEach(function (ev) { canvas.addEventListener(ev, function (e) { dragging = false; canvas.style.cursor = 'grab'; if (ev === 'pointerup' && moved < 5) handleClick(e); }); });
  canvas.addEventListener('wheel', function (e) { e.preventDefault(); if (animCamPos) { animCamPos = null; syncOrbit(); } radius = Math.max(120, Math.min(2800, radius + e.deltaY * 0.7)); updateCamera(); }, { passive: false });

  var raycaster = new THREE.Raycaster(), mouse = new THREE.Vector2(), tooltip = document.getElementById('wh3d-tooltip');
  function pickAt(cx, cy) { var r = canvas.getBoundingClientRect(); mouse.x = ((cx - r.left) / r.width) * 2 - 1; mouse.y = -((cy - r.top) / r.height) * 2 + 1; raycaster.setFromCamera(mouse, camera); var h = raycaster.intersectObjects(interactive, false); return h.length ? h[0].object : null; }
  function hover(e) {
    var o = pickAt(e.clientX, e.clientY);
    if (!o) { tooltip.classList.remove('show'); return; }
    var t = o.userData, wr = wrap.getBoundingClientRect();
    tooltip.style.left = (e.clientX - wr.left + 16) + 'px'; tooltip.style.top = (e.clientY - wr.top + 16) + 'px';
    var status = t.qrCount > 0
      ? '<span class="wh3d-tt-status" style="background:rgba(47,191,122,0.18);color:#2fbf7a">' + I18N.t('statusHasStock') + '</span>'
      : '<span class="wh3d-tt-status" style="background:rgba(148,163,184,0.18);color:#9aa3b5">' + I18N.t('statusEmpty') + '</span>';
    tooltip.innerHTML = '<div class="wh3d-tt-code">' + I18N.t('rackWord') + t.rack + I18N.t('levelWord') + t.level + '</div>' +
      (t.qrCount > 0 ? '<div class="wh3d-tt-row"><span>' + I18N.t('qrCountLabel') + '</span><span>' + t.qrCount + '</span></div>' : '') + status;
    tooltip.classList.add('show');
  }

  // ----- Drawer: danh sách mã QR thật (fetch có phân trang) -----
  var drawer = document.getElementById('wh3d-drawer'), dCode = document.getElementById('wh3d-drawer-code'), dLoc = document.getElementById('wh3d-drawer-loc'), dBody = document.getElementById('wh3d-drawer-body');
  var curLoc = null, curHl = null, curRack = null, curLevel = null;
  function openTierByPad(pad, hlBarcode, startPage) {
    var u = pad.userData; curLoc = u.locationId; curHl = hlBarcode || null; curRack = u.rack; curLevel = u.level;
    dCode.textContent = I18N.t('rackWord') + u.rack + I18N.t('levelWord') + u.level;
    drawer.classList.add('open'); focusPad(pad); showHighlight(pad);
    autoSpin = false; if (btnSpin) { btnSpin.classList.remove('on'); btnSpin.textContent = I18N.t('spinBtnStart'); }
    if (!u.qrCount || !u.locationId) { dLoc.textContent = I18N.t('emptyTier'); dBody.innerHTML = '<div class="wh3d-empty">' + I18N.t('noQrInTier') + '</div>'; return; }
    loadPage(startPage || 1);
  }
  function loadPage(p) {
    dLoc.textContent = I18N.t('feedLoading'); dBody.innerHTML = '<div class="wh3d-empty">' + I18N.t('feedLoading') + '</div>';
    fetch('/Warehouse/LocationMaterials?id=' + curLoc + '&page=' + p)
      .then(function (r) { return r.json(); })
      .then(function (data) { renderDrawer(data); })
      .catch(function () { dBody.innerHTML = '<div class="wh3d-empty">' + I18N.t('loadError') + '</div>'; });
  }
  function renderDrawer(data) {
    var items = data.items || [], total = data.totalPages || 1, page = data.page || 1;
    dLoc.textContent = (data.totalCount != null ? data.totalCount : items.length) + I18N.t('qrInTierSuffix');
    var rows = items.map(function (it) {
      var hl = (curHl && it.barcode === curHl) ? ' class="hl"' : '';
      return '<tr' + hl + '><td class="mono">' + it.barcode + '</td><td>' + (it.dev || '') + '</td><td>' + (it.model || '') + '</td><td class="mono" style="text-align:right">' + (it.balance != null ? it.balance : '') + '</td></tr>';
    }).join('');
    var pager = total > 1 ? '<div class="wh3d-pager"><button id="wh3d-pg-prev"' + (page <= 1 ? ' disabled' : '') + '>‹</button><span>' + I18N.t('pageWord') + page + ' / ' + total + '</span><button id="wh3d-pg-next"' + (page >= total ? ' disabled' : '') + '>›</button></div>' : '';
    dBody.innerHTML = '<span class="wh3d-status">' + I18N.t('statusHasStock') + '</span><table class="wh3d-mini"><thead><tr><th>' + I18N.t('colBarcode') + '</th><th>' + I18N.t('colDev') + '</th><th>' + I18N.t('colModel') + '</th><th style="text-align:right">' + I18N.t('colQty') + '</th></tr></thead><tbody>' + rows + '</tbody></table>' + pager;
    var pv = document.getElementById('wh3d-pg-prev'), nx = document.getElementById('wh3d-pg-next');
    if (pv) pv.onclick = function () { if (page > 1) loadPage(page - 1); };
    if (nx) nx.onclick = function () { if (page < total) loadPage(page + 1); };
  }
  function handleClick(e) { var o = pickAt(e.clientX, e.clientY); if (o) openTierByPad(o, null, 1); }
  document.getElementById('wh3d-drawer-close').addEventListener('click', function () { drawer.classList.remove('open'); hlBox.visible = false; });

  // ----- Tìm barcode -----
  var sInput = document.getElementById('wh3d-search-input'), sMsg = document.getElementById('wh3d-search-msg');
  function setMsg(t, err) { sMsg.textContent = t || ''; sMsg.classList.toggle('err', !!err); }
  function doSearch(q) {
    q = (q || '').trim(); if (!q) return; setMsg(I18N.t('searching'));
    fetch('/Warehouse/Find?barcode=' + encodeURIComponent(q))
      .then(function (r) { return r.json().then(function (d) { return { ok: r.ok, d: d }; }); })
      .then(function (res) {
        if (!res.ok) { setMsg(res.d && res.d.message ? res.d.message : I18N.t('notFound'), true); hlBox.visible = false; return; }
        var f = res.d, pad = padByRackLevel[f.rackNo] && padByRackLevel[f.rackNo][f.levelNo];
        if (!pad) { setMsg(I18N.t('cannotLocate'), true); return; }
        setMsg(I18N.t('foundAt') + f.rackNo + I18N.t('levelWord') + f.levelNo);
        openTierByPad(pad, q, Math.max(1, Math.ceil((f.ordinal || 1) / 8)));
      })
      .catch(function () { setMsg(I18N.t('searchError'), true); });
  }
  document.getElementById('wh3d-search-btn').addEventListener('click', function () { doSearch(sInput.value); });
  sInput.addEventListener('keydown', function (e) { if (e.key === 'Enter') doSearch(sInput.value); });

  function resetView() { animCamPos = null; hlBox.visible = false; target.set(0, 70, 0); radius = 1550; theta = -0.7; phi = 1.0; updateCamera(); }
  var btnReset = document.getElementById('wh3d-btnReset');
  if (btnReset) btnReset.addEventListener('click', resetView);
  var btnSpin = document.getElementById('wh3d-btnSpin');
  if (btnSpin) btnSpin.addEventListener('click', function () {
    autoSpin = !autoSpin;
    btnSpin.classList.toggle('on', autoSpin);
    btnSpin.textContent = autoSpin ? I18N.t('spinBtnStop') : I18N.t('spinBtnStart');
    if (autoSpin) resetView(); // bật tự xoay -> luôn bắt đầu lại từ góc nhìn mặc định
  });

  // ----- Đổi ngôn ngữ: cập nhật lại các phần chữ tĩnh + đang mở (nếu có) sang ngôn ngữ mới -----
  I18N.onChange(function () {
    i18nSignRefreshers.forEach(function (fn) { fn(); }); // vẽ lại chữ trong scene 3D: CỬA RA VÀO, KỆ N, THOÁT HIỂM, màn hình PC
    var badge = document.getElementById('wh3d-badge'); if (badge) badge.innerHTML = I18N.t('dragHint');
    var hint = document.getElementById('wh3d-hint'); if (hint) hint.textContent = I18N.t('tierHint');
    var loadingEl = document.getElementById('wh3d-loading'); if (loadingEl) loadingEl.textContent = I18N.t('loading3d');
    if (sInput) sInput.placeholder = I18N.t('searchPlaceholder');
    var sBtn = document.getElementById('wh3d-search-btn'); if (sBtn) sBtn.textContent = I18N.t('searchBtn');
    if (btnReset) btnReset.textContent = I18N.t('resetBtn');
    if (btnSpin) btnSpin.textContent = autoSpin ? I18N.t('spinBtnStop') : I18N.t('spinBtnStart');
    if (drawer.classList.contains('open') && curRack != null) {
      dCode.textContent = I18N.t('rackWord') + curRack + I18N.t('levelWord') + curLevel;
      if (curLoc) loadPage(1);
    }
  });

  // ----- Realtime (SignalR): tự cập nhật khi có nhập/xuất/nhận lại/hủy -----
  function applyOccupancy(tiers) {
    var occ = 0, qr = 0;
    tiers.forEach(function (t) {
      var obj = tierObjs[t.rackNo + '.' + t.levelNo]; if (!obj) return;
      obj.pad.userData.qrCount = t.qrCount; obj.pad.userData.locationId = t.locationId;
      var has = obj.crates.length > 0;
      if (t.qrCount > 0 && !has) addCrateMeshes(obj);
      else if (t.qrCount === 0 && has) removeCrateMeshes(obj);
      if (t.qrCount > 0) { occ++; qr += t.qrCount; }
    });
    setTxt('wh3d-stat-total', tiers.length); setTxt('wh3d-stat-occ', occ); setTxt('wh3d-stat-qr', qr);
    // nếu đang mở panel 1 tầng, tải lại danh sách mã QR của tầng đó cho khớp
    if (drawer.classList.contains('open') && curLoc) loadPage(1);
  }
  function refreshLayout() {
    fetch('/Warehouse/LayoutData').then(function (r) { return r.json(); }).then(function (d) { applyOccupancy(d || []); }).catch(function () {});
  }
  if (window.signalR) {
    try {
      // Dùng chung 1 kết nối /warehouseHub với warehouse-dashboard.js (window.PmcWhHub) thay vì
      // mỗi file tự mở 1 socket riêng — file nào chạy trước thì tạo, file sau chỉ gắn thêm handler.
      var conn = window.PmcWhHub || new signalR.HubConnectionBuilder().withUrl('/warehouseHub').withAutomaticReconnect().build();
      window.PmcWhHub = conn;
      conn.on('warehouseChanged', refreshLayout);
      if (conn.state === signalR.HubConnectionState.Disconnected) {
        conn.start().catch(function (e) { console.warn('SignalR connect fail', e); });
      }
    } catch (e) { console.warn('SignalR init fail', e); }
  }

  resize(); updateCamera();
  (function loop() {
    requestAnimationFrame(loop);
    if (animCamPos) {
      camera.position.lerp(animCamPos, 0.12); target.lerp(animTarget, 0.12); camera.lookAt(target);
      if (camera.position.distanceTo(animCamPos) < 1.2 && target.distanceTo(animTarget) < 1.2) { animCamPos = null; syncOrbit(); }
    } else if (autoSpin && !reduceMotion) { theta += 0.0025; updateCamera(); }
    renderer.render(scene, camera);
  })();
})();
