/*
 * Theme 3 · the 3D suite. A calm, minimal villa bedroom at night, built in code (no model files):
 * slatted walnut wall, upholstered headboard, linen bed with cushions and a wool throw, two nightstands with a
 * ceramic table lamp and an olive tree, a brass dome pendant, sheer curtains at an arched window with the moon.
 * Materials use small generated textures (wood grain, plaster, linen, wool) and image-based light; the lamps cast
 * soft shadows and the moon throws a patch of light on the floor.
 *
 * buildSuite(THREE, canvas, colors) → { setSize(w, h), setProgress(p 0..1), setPointer(x, y), render(dt), dispose() }
 */
import * as THREE from "/vendor/three/0.160.1/three.module.min.js";
export { THREE };
import { RoundedBoxGeometry } from "/vendor/three/0.160.1/addons/RoundedBoxGeometry.js";
import { RoomEnvironment } from "/vendor/three/0.160.1/addons/RoomEnvironment.js";

/* ---------- small generated textures ---------- */
function rand(seed) {
    var s = seed >>> 0;
    return function () { s = (s * 1664525 + 1013904223) >>> 0; return s / 4294967296; };
}
function canvasTexture(size, draw, repeat) {
    var c = document.createElement("canvas");
    c.width = c.height = size;
    draw(c.getContext("2d"), size);
    var t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    t.wrapS = t.wrapT = THREE.RepeatWrapping;
    if (repeat) t.repeat.set(repeat[0], repeat[1]);
    t.anisotropy = 8;
    return t;
}
function woodTexture(base, repeat, planks) {
    return canvasTexture(512, function (g, n) {
        var r = rand(7);
        g.fillStyle = base; g.fillRect(0, 0, n, n);
        var rows = planks || 6;
        for (var p = 0; p < rows; p++) {
            var y0 = (p / rows) * n, h = n / rows;
            g.fillStyle = "rgba(" + (r() > 0.5 ? "255,235,210," : "40,20,10,") + (0.03 + r() * 0.05) + ")";
            g.fillRect(0, y0, n, h);
            for (var i = 0; i < 26; i++) {
                g.strokeStyle = "rgba(30,15,8," + (0.05 + r() * 0.1) + ")";
                g.lineWidth = 0.6 + r() * 1.4;
                g.beginPath();
                var yy = y0 + r() * h, amp = 1 + r() * 3, ph = r() * 6;
                for (var x = 0; x <= n; x += 16) g.lineTo(x, yy + Math.sin(x / 60 + ph) * amp);
                g.stroke();
            }
            g.fillStyle = "rgba(20,10,5,.45)"; g.fillRect(0, y0, n, 1.5);
            var cut = r() * n; g.fillRect(cut, y0, 1.5, h);
        }
    }, repeat);
}
function noiseTexture(base, amount, repeat, weave) {
    return canvasTexture(256, function (g, n) {
        var r = rand(11);
        g.fillStyle = base; g.fillRect(0, 0, n, n);
        var img = g.getImageData(0, 0, n, n), d = img.data;
        for (var i = 0; i < d.length; i += 4) {
            var px = (i / 4) % n, py = Math.floor(i / 4 / n);
            var v = (r() - 0.5) * amount;
            if (weave) v += ((px % 4 < 2) !== (py % 4 < 2) ? 1 : -1) * weave;
            d[i] += v; d[i + 1] += v; d[i + 2] += v;
        }
        g.putImageData(img, 0, 0);
    }, repeat);
}
function glowTexture() {
    var c = document.createElement("canvas");
    c.width = c.height = 128;
    var g = c.getContext("2d"), grd = g.createRadialGradient(64, 64, 0, 64, 64, 64);
    grd.addColorStop(0, "rgba(255,255,255,1)");
    grd.addColorStop(0.25, "rgba(255,240,210,.55)");
    grd.addColorStop(1, "rgba(255,220,170,0)");
    g.fillStyle = grd; g.fillRect(0, 0, 128, 128);
    var t = new THREE.CanvasTexture(c);
    t.colorSpace = THREE.SRGBColorSpace;
    return t;
}

export function buildSuite(canvas, colors) {
    var renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true, powerPreference: "high-performance" });
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.75));
    renderer.toneMapping = THREE.ACESFilmicToneMapping;
    renderer.toneMappingExposure = 1.0;
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = THREE.PCFSoftShadowMap;

    var scene = new THREE.Scene();
    scene.background = colors.deep.clone();
    scene.fog = new THREE.Fog(colors.deep.clone(), 12, 26);
    var pmrem = new THREE.PMREMGenerator(renderer);
    scene.environment = pmrem.fromScene(new RoomEnvironment(renderer), 0.04).texture;

    var camera = new THREE.PerspectiveCamera(36, 1, 0.1, 80);
    var disposables = [];
    function keep(x) { disposables.push(x); return x; }
    function mesh(geo, mat, x, y, z, cast, receive) {
        var m = new THREE.Mesh(geo, mat);
        m.position.set(x || 0, y || 0, z || 0);
        m.castShadow = cast !== false; m.receiveShadow = receive !== false;
        scene.add(m);
        keep(geo);
        return m;
    }
    function rbox(w, h, d, r, mat, x, y, z) { return mesh(new RoundedBoxGeometry(w, h, d, 4, r), mat, x, y, z); }

    /* ---------- materials ---------- */
    var walnutHex = "#" + colors.floor.clone().lerp(new THREE.Color("#6b4630"), 0.55).getHexString();
    var floorMat = keep(new THREE.MeshStandardMaterial({ map: woodTexture(walnutHex, [3, 3], 7), roughness: 0.55, metalness: 0 }));
    var slatMat = keep(new THREE.MeshStandardMaterial({ map: woodTexture("#" + colors.floor.clone().lerp(new THREE.Color("#7a5236"), 0.6).getHexString(), [0.2, 1], 1), roughness: 0.6 }));
    var walnut = keep(new THREE.MeshStandardMaterial({ map: woodTexture("#5a3a27", [1, 1], 3), roughness: 0.5 }));
    var plaster = keep(new THREE.MeshStandardMaterial({ map: noiseTexture("#" + colors.wall.getHexString(), 14, [4, 2]), roughness: 0.95 }));
    var linen = keep(new THREE.MeshStandardMaterial({ map: noiseTexture("#ece5da", 10, [6, 6], 4), roughness: 0.92 }));
    var duvetMat = keep(new THREE.MeshStandardMaterial({ map: noiseTexture("#f4efe7", 8, [5, 5], 3), roughness: 0.95 }));
    var headboardMat = keep(new THREE.MeshPhysicalMaterial({ color: colors.wall.clone().lerp(new THREE.Color("#b9a58c"), 0.55), roughness: 0.9, sheen: 1, sheenRoughness: 0.6, sheenColor: new THREE.Color("#e8d8c0") }));
    var throwMat = keep(new THREE.MeshPhysicalMaterial({ map: noiseTexture("#" + colors.brass.clone().lerp(new THREE.Color("#8a5a2b"), 0.35).getHexString(), 26, [4, 4], 6), roughness: 1, sheen: 0.6, sheenColor: colors.brass2 }));
    var sage = keep(new THREE.MeshPhysicalMaterial({ color: new THREE.Color("#8d9a82"), roughness: 0.95, sheen: 0.5, sheenColor: new THREE.Color("#dfe6d6") }));
    var brass = keep(new THREE.MeshStandardMaterial({ color: colors.brass, roughness: 0.28, metalness: 1 }));
    var ceramic = keep(new THREE.MeshStandardMaterial({ color: new THREE.Color("#d9cfc2"), roughness: 0.45 }));
    var stone = keep(new THREE.MeshStandardMaterial({ color: new THREE.Color("#3a332e"), roughness: 0.7 }));
    var woolRug = keep(new THREE.MeshStandardMaterial({ map: noiseTexture("#" + colors.wall.clone().lerp(new THREE.Color("#d8ccb8"), 0.55).getHexString(), 30, [3, 3]), roughness: 1 }));
    var glow = keep(glowTexture());

    /* ---------- room shell ---------- */
    var floor = mesh(new THREE.PlaneGeometry(16, 16), floorMat, 0, 0, 0, false, true);
    floor.rotation.x = -Math.PI / 2;
    var ceiling = mesh(new THREE.PlaneGeometry(16, 16), plaster, 0, 5.2, 0, false, true);
    ceiling.rotation.x = Math.PI / 2;
    var left = mesh(new THREE.PlaneGeometry(16, 5.2), plaster, -4.6, 2.6, 0, false, true);
    left.rotation.y = Math.PI / 2;

    // Back wall with an arched window opening, deep reveal.
    var WX = 2.15, ARCH_R = 1.15, SILL = 0.75, SPRING = 3.0;
    function archPath(path, r, bottom) {
        path.moveTo(WX - r, bottom); path.lineTo(WX + r, bottom); path.lineTo(WX + r, SPRING);
        path.absarc(WX, SPRING, r, 0, Math.PI, false); path.lineTo(WX - r, bottom);
        return path;
    }
    var wallShape = new THREE.Shape();
    wallShape.moveTo(-8, 0); wallShape.lineTo(8, 0); wallShape.lineTo(8, 5.2); wallShape.lineTo(-8, 5.2); wallShape.lineTo(-8, 0);
    wallShape.holes.push(archPath(new THREE.Path(), ARCH_R, SILL));
    var wall = mesh(new THREE.ExtrudeGeometry(wallShape, { depth: 0.45, bevelEnabled: false, curveSegments: 64 }), plaster, 0, 0, -4.45);
    wall.castShadow = true;
    var trim = new THREE.Shape(); archPath(trim, ARCH_R + 0.06, SILL - 0.04);
    trim.holes.push(archPath(new THREE.Path(), ARCH_R, SILL));
    mesh(new THREE.ExtrudeGeometry(trim, { depth: 0.03, bevelEnabled: false, curveSegments: 64 }), brass, 0, 0, -3.995);
    rbox(ARCH_R * 2 + 0.3, 0.07, 0.55, 0.02, stone, WX, SILL - 0.035, -4.0);
    // Slim brass mullions.
    rbox(0.025, SPRING + ARCH_R - SILL, 0.025, 0.01, brass, WX, (SPRING + ARCH_R + SILL) / 2, -4.25);
    rbox(ARCH_R * 2, 0.025, 0.025, 0.01, brass, WX, SPRING, -4.25);
    var glass = mesh(new THREE.ShapeGeometry(archPath(new THREE.Shape(), ARCH_R, SILL), 48), keep(new THREE.MeshPhysicalMaterial({ color: 0xbfd0ff, transparent: true, opacity: 0.08, roughness: 0.05, metalness: 0 })), 0, 0, -4.24, false, false);
    void glass;

    // Slatted walnut feature wall behind the bed.
    for (var s = 0; s < 34; s++) {
        rbox(0.07, 3.6, 0.06, 0.015, slatMat, -3.9 + s * 0.11, 1.8, -3.95);
    }

    /* ---------- bed ---------- */
    var BX = -2.05, BZ = -2.55;
    rbox(2.5, 1.35, 0.16, 0.06, headboardMat, BX, 1.05, -3.78);
    for (var t = 0; t < 6; t++) rbox(0.39, 1.22, 0.08, 0.04, headboardMat, BX - 1.0 + t * 0.4, 1.06, -3.68);
    rbox(2.45, 0.32, 2.35, 0.05, walnut, BX, 0.2, BZ);
    rbox(2.3, 0.26, 2.2, 0.1, linen, BX, 0.5, BZ);
    var duvet = rbox(2.42, 0.12, 1.65, 0.06, duvetMat, BX, 0.66, BZ + 0.3);
    duvet.rotation.x = -0.012;
    rbox(2.5, 0.07, 0.62, 0.035, throwMat, BX, 0.74, BZ + 0.72);
    var pillowL = rbox(0.95, 0.24, 0.5, 0.11, linen, BX - 0.56, 0.78, -3.42); pillowL.rotation.x = -0.35;
    var pillowR = rbox(0.95, 0.24, 0.5, 0.11, linen, BX + 0.56, 0.78, -3.42); pillowR.rotation.x = -0.35;
    var cushion1 = rbox(0.55, 0.2, 0.5, 0.09, sage, BX - 0.32, 0.86, -3.12); cushion1.rotation.set(-0.5, 0.12, 0);
    var cushion2 = rbox(0.5, 0.18, 0.45, 0.08, throwMat, BX + 0.38, 0.85, -3.1); cushion2.rotation.set(-0.45, -0.18, 0);

    /* ---------- nightstands, lamp, decor ---------- */
    function nightstand(x) {
        rbox(0.62, 0.52, 0.48, 0.04, walnut, x, 0.32, -3.5);
        rbox(0.52, 0.02, 0.4, 0.008, brass, x, 0.3, -3.255);
        rbox(0.46, 0.06, 0.06, 0.02, walnut, x, 0.06, -3.5);
    }
    nightstand(BX - 1.62);
    nightstand(BX + 1.62);
    // Ceramic table lamp with a linen shade (right).
    var lampBase = new THREE.LatheGeometry([new THREE.Vector2(0, 0), new THREE.Vector2(0.11, 0.02), new THREE.Vector2(0.15, 0.12), new THREE.Vector2(0.12, 0.26), new THREE.Vector2(0.04, 0.34), new THREE.Vector2(0.02, 0.42)], 40);
    mesh(lampBase, ceramic, BX + 1.62, 0.58, -3.55);
    var shadeMat = keep(new THREE.MeshStandardMaterial({ color: new THREE.Color("#f3e6cf"), emissive: colors.brass2, emissiveIntensity: 0.9, roughness: 0.9, side: THREE.DoubleSide, transparent: true, opacity: 0.96 }));
    var shade = mesh(new THREE.CylinderGeometry(0.17, 0.24, 0.3, 40, 1, true), shadeMat, BX + 1.62, 1.1, -3.55, false, false);
    void shade;
    var tableLight = new THREE.PointLight(colors.brass2, 4, 7, 1.8);
    tableLight.position.set(BX + 1.62, 1.05, -3.4);
    tableLight.castShadow = true; tableLight.shadow.mapSize.set(1024, 1024); tableLight.shadow.radius = 6; tableLight.shadow.bias = -0.002;
    scene.add(tableLight);
    // Books and a small vase (left).
    rbox(0.32, 0.05, 0.24, 0.01, keep(new THREE.MeshStandardMaterial({ color: "#7b6a58", roughness: 0.8 })), BX - 1.66, 0.61, -3.5);
    rbox(0.28, 0.04, 0.22, 0.01, keep(new THREE.MeshStandardMaterial({ color: "#a89880", roughness: 0.8 })), BX - 1.64, 0.655, -3.5);
    var vase = new THREE.LatheGeometry([new THREE.Vector2(0, 0), new THREE.Vector2(0.06, 0.01), new THREE.Vector2(0.08, 0.1), new THREE.Vector2(0.04, 0.2), new THREE.Vector2(0.03, 0.26)], 32);
    mesh(vase, stone, BX - 1.52, 0.675, -3.56);

    // Olive tree in a ceramic pot (front left corner), leaves as instances.
    var potGeo = new THREE.LatheGeometry([new THREE.Vector2(0, 0), new THREE.Vector2(0.24, 0), new THREE.Vector2(0.3, 0.12), new THREE.Vector2(0.32, 0.46), new THREE.Vector2(0.29, 0.5)], 48);
    var PX = -3.85, PZ = -1.2;
    mesh(potGeo, ceramic, PX, 0, PZ);
    var trunkMat = keep(new THREE.MeshStandardMaterial({ color: "#4a3b2c", roughness: 0.9 }));
    var branches = [];
    var rr = rand(5);
    function branch(from, dir, len, radius, depth) {
        var to = from.clone().add(dir.clone().multiplyScalar(len));
        var geo = new THREE.CylinderGeometry(radius * 0.7, radius, len, 8);
        var m = mesh(geo, trunkMat, 0, 0, 0);
        m.position.copy(from).lerp(to, 0.5);
        m.quaternion.setFromUnitVectors(new THREE.Vector3(0, 1, 0), dir.clone().normalize());
        branches.push(to);
        if (depth > 0) {
            for (var k = 0; k < 2; k++) {
                var nd = dir.clone().add(new THREE.Vector3((rr() - 0.5) * 1.1, 0.25 + rr() * 0.3, (rr() - 0.5) * 1.1)).normalize();
                branch(to, nd, len * 0.72, radius * 0.65, depth - 1);
            }
        }
    }
    branch(new THREE.Vector3(PX, 0.45, PZ), new THREE.Vector3(0.05, 1, 0.02), 0.9, 0.05, 3);
    var leafShape = new THREE.Shape();
    leafShape.moveTo(0, 0); leafShape.quadraticCurveTo(0.03, 0.06, 0, 0.16); leafShape.quadraticCurveTo(-0.03, 0.06, 0, 0);
    var leafGeo = keep(new THREE.ShapeGeometry(leafShape, 4));
    var leafMat = keep(new THREE.MeshStandardMaterial({ color: "#6d7a55", roughness: 0.8, side: THREE.DoubleSide }));
    var leaves = new THREE.InstancedMesh(leafGeo, leafMat, branches.length * 26);
    leaves.castShadow = true;
    var dummy = new THREE.Object3D(), li = 0, tint = new THREE.Color();
    branches.forEach(function (b, bi) {
        if (bi < 2) return;
        for (var q = 0; q < 26; q++) {
            dummy.position.set(b.x + (rr() - 0.5) * 0.42, b.y + (rr() - 0.4) * 0.34, b.z + (rr() - 0.5) * 0.42);
            dummy.rotation.set(rr() * Math.PI, rr() * Math.PI, rr() * Math.PI);
            var sc = 0.8 + rr() * 0.6; dummy.scale.set(sc, sc, sc);
            dummy.updateMatrix();
            leaves.setMatrixAt(li, dummy.matrix);
            leaves.setColorAt(li, tint.set("#6d7a55").offsetHSL((rr() - 0.5) * 0.04, 0, (rr() - 0.5) * 0.12));
            li++;
        }
    });
    leaves.count = li;
    scene.add(leaves);

    // Wool rug.
    var rugShape = new THREE.Shape();
    var RW = 3.4, RD = 2.4, RR = 0.25;
    rugShape.moveTo(-RW / 2 + RR, -RD / 2); rugShape.lineTo(RW / 2 - RR, -RD / 2); rugShape.quadraticCurveTo(RW / 2, -RD / 2, RW / 2, -RD / 2 + RR);
    rugShape.lineTo(RW / 2, RD / 2 - RR); rugShape.quadraticCurveTo(RW / 2, RD / 2, RW / 2 - RR, RD / 2); rugShape.lineTo(-RW / 2 + RR, RD / 2);
    rugShape.quadraticCurveTo(-RW / 2, RD / 2, -RW / 2, RD / 2 - RR); rugShape.lineTo(-RW / 2, -RD / 2 + RR); rugShape.quadraticCurveTo(-RW / 2, -RD / 2, -RW / 2 + RR, -RD / 2);
    var rug = mesh(new THREE.ExtrudeGeometry(rugShape, { depth: 0.025, bevelEnabled: true, bevelSize: 0.015, bevelThickness: 0.01, bevelSegments: 2 }), woolRug, BX + 0.2, 0.002, -1.2, false, true);
    rug.rotation.x = -Math.PI / 2;

    // Bench at the foot of the bed.
    rbox(1.6, 0.08, 0.45, 0.03, walnut, BX, 0.44, -0.95);
    [-0.7, 0.7].forEach(function (dx) { rbox(0.05, 0.42, 0.4, 0.02, walnut, BX + dx, 0.21, -0.95); });
    rbox(0.6, 0.08, 0.4, 0.035, throwMat, BX + 0.35, 0.52, -0.95);

    /* ---------- pendant ---------- */
    var PNX = BX + 1.62, PNY = 2.55, PNZ = -2.7;
    mesh(new THREE.CylinderGeometry(0.006, 0.006, 5.2 - PNY - 0.18, 6), stone, PNX, (5.2 + PNY + 0.18) / 2, PNZ, false, false);
    var dome = new THREE.LatheGeometry([new THREE.Vector2(0.03, 0.2), new THREE.Vector2(0.12, 0.17), new THREE.Vector2(0.22, 0.08), new THREE.Vector2(0.27, 0), new THREE.Vector2(0.265, -0.01)], 48);
    var domeMesh = mesh(dome, keep(new THREE.MeshStandardMaterial({ color: colors.brass, roughness: 0.3, metalness: 1, side: THREE.DoubleSide })), PNX, PNY, PNZ);
    var bulbMat = keep(new THREE.MeshBasicMaterial({ color: colors.brass2 }));
    mesh(new THREE.SphereGeometry(0.06, 24, 16), bulbMat, PNX, PNY + 0.02, PNZ, false, false);
    var pendantLight = new THREE.PointLight(colors.brass2, 5, 9, 1.6);
    pendantLight.position.set(PNX, PNY - 0.05, PNZ);
    pendantLight.castShadow = true; pendantLight.shadow.mapSize.set(1024, 1024); pendantLight.shadow.radius = 8; pendantLight.shadow.bias = -0.002;
    scene.add(pendantLight);

    function glowSprite(x, y, z, size, opacity) {
        var sp = new THREE.Sprite(keep(new THREE.SpriteMaterial({ map: glow, color: colors.brass2, transparent: true, opacity: opacity, depthWrite: false, blending: THREE.AdditiveBlending })));
        sp.position.set(x, y, z); sp.scale.set(size, size, 1);
        scene.add(sp);
        return sp;
    }
    var pendantGlow = glowSprite(PNX, PNY - 0.06, PNZ, 1.1, 0.5);
    var tableGlow = glowSprite(BX + 1.62, 1.1, -3.5, 1.3, 0.45);

    /* ---------- outside: sky, moon, palms ---------- */
    var skyMat = keep(new THREE.ShaderMaterial({
        uniforms: { top: { value: colors.skyTop }, low: { value: colors.skyLow }, time: { value: 0 } },
        vertexShader: "varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }",
        fragmentShader: [
            "uniform vec3 top; uniform vec3 low; uniform float time; varying vec2 vUv;",
            "float h(vec2 p){ return fract(sin(dot(p, vec2(12.9898, 78.233))) * 43758.5453); }",
            "void main(){ vec3 c = mix(low, top, smoothstep(0.05, 0.85, vUv.y));",
            "  vec2 g = vUv * vec2(160.0, 90.0); vec2 id = floor(g); vec2 f = fract(g) - 0.5;",
            "  float s = step(0.992, h(id)) * smoothstep(0.12, 0.0, length(f)) * smoothstep(0.35, 0.8, vUv.y);",
            "  c += s * (0.6 + 0.4 * sin(time * 1.5 + h(id + 3.0) * 30.0));",
            "  gl_FragColor = vec4(c, 1.0); }"
        ].join("\n"),
        fog: false
    }));
    mesh(new THREE.PlaneGeometry(40, 18), skyMat, WX, 6, -16, false, false);
    var moonMat = keep(new THREE.MeshBasicMaterial({ color: colors.brass2.clone().lerp(new THREE.Color("#fff6e4"), 0.6), fog: false }));
    mesh(new THREE.CircleGeometry(0.62, 64), moonMat, WX + 0.7, 4.7, -15.8, false, false);
    var moonGlow = new THREE.Sprite(keep(new THREE.SpriteMaterial({ map: glow, color: colors.brass2, transparent: true, opacity: 0.35, depthWrite: false, fog: false })));
    moonGlow.position.set(WX + 0.7, 4.7, -15.7); moonGlow.scale.set(4.5, 4.5, 1); scene.add(moonGlow);
    var hills = new THREE.Shape();
    hills.moveTo(-20, 0); hills.bezierCurveTo(-10, 2.2, -4, 0.8, 1, 1.8); hills.bezierCurveTo(6, 2.8, 12, 1.0, 20, 2.0); hills.lineTo(20, -3); hills.lineTo(-20, -3);
    var silhouette = keep(new THREE.MeshBasicMaterial({ color: colors.deep.clone().lerp(colors.skyLow, 0.18), fog: false }));
    mesh(new THREE.ShapeGeometry(hills, 24), silhouette, WX, -0.2, -15.5, false, false);
    function palm(x, h, lean) {
        var trunk = mesh(new THREE.CylinderGeometry(0.05, 0.08, h, 6), silhouette, x, h / 2 - 0.2, -14.8, false, false);
        trunk.rotation.z = lean;
        var top = new THREE.Vector3(x - Math.sin(lean) * h / 2, h / 2 - 0.2 + Math.cos(lean) * h / 2, -14.8);
        for (var f = 0; f < 7; f++) {
            var frond = new THREE.Shape();
            frond.moveTo(0, 0); frond.quadraticCurveTo(0.6, 0.25, 1.3, -0.25); frond.quadraticCurveTo(0.6, 0.08, 0, 0);
            var m = mesh(new THREE.ShapeGeometry(frond, 6), silhouette, top.x, top.y, top.z, false, false);
            m.rotation.z = (f / 7) * Math.PI * 2;
        }
    }
    palm(WX + 2.4, 3.4, 0.12);
    palm(WX - 1.9, 2.8, -0.08);

    /* ---------- curtains ---------- */
    var curtainMat = keep(new THREE.MeshStandardMaterial({ color: new THREE.Color("#efe7da"), roughness: 1, transparent: true, opacity: 0.82, side: THREE.DoubleSide }));
    var curtains = [];
    [-1, 1].forEach(function (side) {
        var geo = new THREE.PlaneGeometry(0.75, 4.1, 40, 1);
        var p = geo.attributes.position;
        for (var v = 0; v < p.count; v++) { var x = p.getX(v); p.setZ(v, Math.sin(x * 26) * 0.045); }
        geo.computeVertexNormals();
        var m = mesh(geo, curtainMat, WX + side * (ARCH_R + 0.42), 2.33, -3.85, true, false);
        curtains.push(m);
    });
    rbox(ARCH_R * 2 + 1.9, 0.03, 0.03, 0.012, brass, WX, 4.4, -3.85);

    /* ---------- lights ---------- */
    var moon = new THREE.DirectionalLight(colors.skyLow.clone().lerp(new THREE.Color("#c8d4ff"), 0.6), 0.6);
    moon.position.set(WX + 1.5, 13, -13);
    moon.target.position.set(WX - 0.6, 0, -2.2);
    moon.castShadow = true; moon.shadow.mapSize.set(1024, 1024); moon.shadow.radius = 4;
    moon.shadow.camera.left = -12; moon.shadow.camera.right = 12; moon.shadow.camera.top = 12; moon.shadow.camera.bottom = -12; moon.shadow.camera.far = 40; moon.shadow.mapSize.set(2048, 2048); moon.shadow.radius = 10; moon.shadow.bias = -0.0015;
    scene.add(moon); scene.add(moon.target);
    scene.add(new THREE.HemisphereLight(colors.skyLow.clone().lerp(new THREE.Color("#ffffff"), 0.2), colors.floor, 0.25));

    /* ---------- dust in the lamplight ---------- */
    var DUST = 220, dpos = new Float32Array(DUST * 3), dr = rand(3);
    for (var d = 0; d < DUST; d++) { dpos[d * 3] = BX - 0.5 + dr() * 4; dpos[d * 3 + 1] = 0.4 + dr() * 3.6; dpos[d * 3 + 2] = -3.6 + dr() * 3.4; }
    var dustGeo = keep(new THREE.BufferGeometry());
    dustGeo.setAttribute("position", new THREE.BufferAttribute(dpos, 3));
    var dust = new THREE.Points(dustGeo, keep(new THREE.PointsMaterial({ map: glow, color: colors.brass2, size: 0.035, transparent: true, opacity: 0.55, depthWrite: false, blending: THREE.AdditiveBlending, sizeAttenuation: true })));
    scene.add(dust);

    // Image-based light only as a faint fill: it is night.
    scene.traverse(function (o) {
        var m = o.material;
        if (m && "envMapIntensity" in m) m.envMapIntensity = m.metalness > 0.5 ? 0.55 : 0.16;
    });

    /* ---------- camera path ---------- */
    var from = { x: 0.9, y: 2.1, z: 9.5, tx: -0.7, ty: 1.5, tz: -3.6 };
    var to = { x: -0.2, y: 1.45, z: 2.6, tx: -1.3, ty: 0.95, tz: -3.4 };
    var progress = 0, pointer = { x: 0, y: 0 }, eased = { x: 0, y: 0 }, time = 0, width = 1, height = 1;
    var look = new THREE.Vector3();

    function place() {
        var k = progress * progress * (3 - 2 * progress);
        camera.position.set(from.x + (to.x - from.x) * k + eased.x * 0.5, from.y + (to.y - from.y) * k + eased.y * 0.25, from.z + (to.z - from.z) * k);
        look.set(from.tx + (to.tx - from.tx) * k + eased.x * 0.2, from.ty + (to.ty - from.ty) * k, from.tz + (to.tz - from.tz) * k);
        camera.lookAt(look);
        // Keep the room to the right of the text column at the start, centred once inside.
        var shift = (1 - k) * 0.14;
        camera.setViewOffset(width * (1 + shift), height, -width * shift, 0, width, height);
        var warm = 0.55 + 0.45 * k;
        pendantLight.intensity = 5 * warm + 2;
        tableLight.intensity = 4 * warm;
        pendantGlow.material.opacity = 0.35 + 0.25 * k;
        renderer.toneMappingExposure = 0.95 + 0.15 * k;
    }

    return {
        setSize: function (w, h) {
            width = w; height = h;
            renderer.setSize(w, h, false);
            camera.aspect = w / Math.max(h, 1);
            camera.updateProjectionMatrix();
        },
        setProgress: function (p) { progress = Math.max(0, Math.min(1, p)); },
        setPointer: function (x, y) { pointer.x = x; pointer.y = y; },
        render: function (dt) {
            time += dt;
            eased.x += (pointer.x - eased.x) * 0.045;
            eased.y += (pointer.y - eased.y) * 0.045;
            place();
            skyMat.uniforms.time.value = time;
            dust.rotation.y = Math.sin(time * 0.05) * 0.15;
            dust.position.y = Math.sin(time * 0.25) * 0.06;
            curtains.forEach(function (c, i) { c.rotation.y = Math.sin(time * 0.5 + i * 1.7) * 0.025; });
            domeMesh.rotation.z = Math.sin(time * 0.4) * 0.01;
            tableGlow.material.opacity = 0.42 + Math.sin(time * 1.3) * 0.02;
            renderer.render(scene, camera);
        },
        dispose: function () {
            disposables.forEach(function (x) { if (x && x.dispose) x.dispose(); });
            pmrem.dispose(); renderer.dispose();
        }
    };
}
