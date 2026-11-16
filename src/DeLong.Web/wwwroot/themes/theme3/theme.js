/*
 * Theme 3 · Premium 3D.
 *  - Hero: on capable desktops a small villa room is built with three.js (loaded on demand). The hero holds still while
 *    the visitor scrolls and the camera walks in through the arch; the lamp warms up; the pointer tilts the view.
 *    Elsewhere (phones, reduced motion, no WebGL) the same room is a brass line drawing.
 *  - Brass line drawings draw themselves when scrolled into view; story arches drift at different depths;
 *    real numbers count up; rooms slide sideways while their section is held (desktop) and lean toward the pointer.
 * Booking, calendar and payment scripts are shared by every theme and untouched.
 */
(function () {
    "use strict";
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var gsap = window.gsap;
    var ST = window.ScrollTrigger;
    var THREE_URL = "/vendor/three/0.160.1/three.module.min.js";

    function desktop3d() {
        if (reduceMotion) return false;
        if (!window.matchMedia("(min-width: 1024px) and (hover: hover) and (pointer: fine)").matches) return false;
        var conn = navigator.connection;
        if (conn && (conn.saveData || /(^|-)2g$/.test(conn.effectiveType || ""))) return false;
        try {
            var probe = document.createElement("canvas");
            return !!(probe.getContext("webgl2") || probe.getContext("webgl"));
        } catch (e) { return false; }
    }

    function cssVar(el, name, fallback) {
        var v = getComputedStyle(el).getPropertyValue(name).trim();
        return v || fallback;
    }

    /* ---------- line drawings ---------- */
    function prepareLines(svg) {
        var shapes = svg.querySelectorAll("path, circle");
        shapes.forEach(function (shape) {
            var len = shape.getTotalLength ? shape.getTotalLength() : 0;
            if (!len) return;
            shape.style.strokeDasharray = len;
            shape.style.strokeDashoffset = len;
        });
        return shapes;
    }
    function drawings() {
        document.querySelectorAll("[data-t3-draw-now]").forEach(function (svg) {
            var shapes = prepareLines(svg);
            gsap.to(shapes, { strokeDashoffset: 0, duration: 2.6, ease: "power2.inOut", stagger: 0.12, delay: 0.2 });
        });
        document.querySelectorAll("[data-t3-draw]").forEach(function (svg) {
            var shapes = prepareLines(svg);
            gsap.to(shapes, { strokeDashoffset: 0, duration: 2.4, ease: "power2.inOut", stagger: 0.2, scrollTrigger: { trigger: svg, start: "top 80%", once: true } });
        });
    }

    /* ---------- hero text ---------- */
    function heroText() {
        var lines = document.querySelectorAll("[data-t3-line]");
        if (!lines.length) return;
        gsap.from(lines, { opacity: 0, y: 40, duration: 1.4, ease: "power3.out", stagger: 0.14, delay: 0.25, clearProps: "opacity,transform" });
    }

    /* ---------- story: drifting arches + counters ---------- */
    function storyMotion() {
        document.querySelectorAll(".t3-story").forEach(function (section) {
            section.querySelectorAll("[data-t3-speed]").forEach(function (el) {
                var speed = parseFloat(el.getAttribute("data-t3-speed") || "0.3");
                gsap.fromTo(el, { y: speed * 160 }, { y: speed * -160, ease: "none", scrollTrigger: { trigger: section, start: "top bottom", end: "bottom top", scrub: 0.8 } });
            });
        });
        document.querySelectorAll("[data-t3-count]").forEach(function (el) {
            var target = parseFloat(el.getAttribute("data-t3-count"));
            var suffix = el.getAttribute("data-t3-suffix") || "";
            if (!isFinite(target)) return;
            var state = { n: 0 };
            el.textContent = "0" + suffix;
            gsap.to(state, {
                n: target, duration: 1.8, ease: "power2.out",
                scrollTrigger: { trigger: el, start: "top 85%", once: true },
                onUpdate: function () { el.textContent = Math.round(state.n).toLocaleString("vi-VN") + suffix; }
            });
        });
    }

    /* ---------- rooms: held section, sideways track, tilt ---------- */
    function rooms() {
        var mm = gsap.matchMedia();
        mm.add("(min-width: 1024px)", function () {
            var section = document.querySelector("[data-t3-rooms]");
            var track = section && section.querySelector("[data-t3-track]");
            if (!track) return;
            section.classList.add("is-pinned");
            var distance = function () { return Math.max(0, track.scrollWidth - window.innerWidth); };
            if (distance() < 40) { section.classList.remove("is-pinned"); return; }
            var tween = gsap.to(track, {
                x: function () { return -distance(); }, ease: "none",
                scrollTrigger: { trigger: section, start: "top top", end: function () { return "+=" + distance(); }, pin: true, scrub: 0.8, invalidateOnRefresh: true }
            });
            return function () { tween.scrollTrigger && tween.scrollTrigger.kill(); tween.kill(); gsap.set(track, { clearProps: "x" }); section.classList.remove("is-pinned"); };
        });
        if (!window.matchMedia("(hover: hover) and (pointer: fine)").matches) return;
        document.querySelectorAll("[data-t3-tilt]").forEach(function (card) {
            var rx = gsap.quickTo(card, "rotationX", { duration: 0.6, ease: "power3.out" });
            var ry = gsap.quickTo(card, "rotationY", { duration: 0.6, ease: "power3.out" });
            card.addEventListener("pointermove", function (e) {
                var r = card.getBoundingClientRect();
                ry(((e.clientX - r.left) / r.width - 0.5) * 10);
                rx(-((e.clientY - r.top) / r.height - 0.5) * 8);
            });
            card.addEventListener("pointerleave", function () { rx(0); ry(0); });
        });
    }

    /* ---------- the 3D room ---------- */
    function room3d() {
        var hero = document.querySelector("[data-t3-hero]");
        var canvas = hero && hero.querySelector("[data-t3-canvas]");
        if (!canvas || !desktop3d()) return;
        var start = function () {
            import(THREE_URL).then(function (THREE) { buildRoom(THREE, hero, canvas); }).catch(function () { /* line drawing stays */ });
        };
        if ("requestIdleCallback" in window) window.requestIdleCallback(start, { timeout: 1200 });
        else window.setTimeout(start, 200);
    }

    function buildRoom(THREE, hero, canvas) {
        var c = function (name, fallback) { return new THREE.Color(cssVar(hero, name, fallback)); };
        var colors = {
            deep: c("--t3-deep", "#17110e"), wall: c("--t3-wall", "#4a382d"), floor: c("--t3-floor", "#2b1f19"),
            brass: c("--t3-brass", "#c9a46a"), brass2: c("--t3-brass-2", "#e3c894"), ivory: c("--t3-ivory", "#f3ece2"),
            skyTop: c("--t3-sky-top", "#0f1426"), skyLow: c("--t3-sky-low", "#3a3550")
        };
        var renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: true, powerPreference: "high-performance" });
        renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.75));
        renderer.toneMapping = THREE.ACESFilmicToneMapping;
        renderer.toneMappingExposure = 1.05;

        var scene = new THREE.Scene();
        scene.background = colors.deep.clone();
        scene.fog = new THREE.Fog(colors.deep.clone(), 9, 22);
        var camera = new THREE.PerspectiveCamera(38, 1, 0.1, 60);

        function std(color, rough, metal) { return new THREE.MeshStandardMaterial({ color: color, roughness: rough == null ? 0.85 : rough, metalness: metal || 0 }); }
        function add(geo, mat, x, y, z) { var m = new THREE.Mesh(geo, mat); m.position.set(x || 0, y || 0, z || 0); scene.add(m); return m; }

        // Room shell: floor, back wall with an arched opening, two side walls, ceiling.
        add(new THREE.PlaneGeometry(14, 14), std(colors.floor, 0.7), 0, 0, 0).rotation.x = -Math.PI / 2;
        var wallShape = new THREE.Shape();
        wallShape.moveTo(-7, 0); wallShape.lineTo(7, 0); wallShape.lineTo(7, 6); wallShape.lineTo(-7, 6); wallShape.lineTo(-7, 0);
        var arch = new THREE.Path();
        arch.moveTo(-1.4, 0.55); arch.lineTo(1.4, 0.55); arch.lineTo(1.4, 3.1); arch.absarc(0, 3.1, 1.4, 0, Math.PI, false); arch.lineTo(-1.4, 0.55);
        wallShape.holes.push(arch);
        var wall = add(new THREE.ExtrudeGeometry(wallShape, { depth: 0.35, bevelEnabled: false, curveSegments: 48 }), std(colors.wall, 0.95), 0.6, 0, -4.35);
        var trimShape = new THREE.Shape();
        trimShape.moveTo(-1.5, 0.5); trimShape.lineTo(1.5, 0.5); trimShape.lineTo(1.5, 3.1); trimShape.absarc(0, 3.1, 1.5, 0, Math.PI, false); trimShape.lineTo(-1.5, 0.5);
        var trimHole = new THREE.Path();
        trimHole.moveTo(-1.4, 0.55); trimHole.lineTo(1.4, 0.55); trimHole.lineTo(1.4, 3.1); trimHole.absarc(0, 3.1, 1.4, 0, Math.PI, false); trimHole.lineTo(-1.4, 0.55);
        trimShape.holes.push(trimHole);
        add(new THREE.ExtrudeGeometry(trimShape, { depth: 0.06, bevelEnabled: false, curveSegments: 48 }), std(colors.brass, 0.35, 0.8), 0.6, 0, -3.98);
        add(new THREE.BoxGeometry(3.0, 0.08, 0.5), std(colors.brass, 0.4, 0.7), 0.6, 0.55, -4.05);
        add(new THREE.PlaneGeometry(14, 6), std(colors.wall.clone().multiplyScalar(0.8), 1), -5.2, 3, 0).rotation.y = Math.PI / 2;
        add(new THREE.PlaneGeometry(14, 6), std(colors.wall.clone().multiplyScalar(0.8), 1), 6.4, 3, 0).rotation.y = -Math.PI / 2;
        add(new THREE.PlaneGeometry(14, 14), std(colors.wall.clone().multiplyScalar(0.55), 1), 0, 6, 0).rotation.x = Math.PI / 2;

        // Night outside the window: gradient sky, moon, a dark ridge.
        var sky = new THREE.ShaderMaterial({
            uniforms: { top: { value: colors.skyTop }, low: { value: colors.skyLow } },
            vertexShader: "varying vec2 vUv; void main(){ vUv = uv; gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0); }",
            fragmentShader: "uniform vec3 top; uniform vec3 low; varying vec2 vUv; void main(){ gl_FragColor = vec4(mix(low, top, smoothstep(0.1, 0.9, vUv.y)), 1.0); }",
            fog: false
        });
        add(new THREE.PlaneGeometry(30, 16), sky, 0.6, 5, -12);
        var moon = add(new THREE.CircleGeometry(0.75, 48), new THREE.MeshBasicMaterial({ color: colors.brass2, fog: false }), 1.6, 4.6, -11.8);
        var halo = add(new THREE.CircleGeometry(2.4, 48), new THREE.MeshBasicMaterial({ color: colors.brass2, transparent: true, opacity: 0.08, fog: false }), 1.6, 4.6, -11.9);
        var ridge = new THREE.Shape();
        ridge.moveTo(-15, 0); ridge.bezierCurveTo(-8, 2.6, -3, 1.2, 1, 2.2); ridge.bezierCurveTo(5, 3.2, 9, 1.4, 15, 2.4); ridge.lineTo(15, -2); ridge.lineTo(-15, -2);
        add(new THREE.ShapeGeometry(ridge), new THREE.MeshBasicMaterial({ color: colors.deep.clone().lerp(colors.skyLow, 0.25), fog: false }), 0.6, -0.4, -11.5);

        // Furniture: low bed with linen and a brass throw, side table with a plant, rug.
        var linen = std(colors.ivory, 0.9);
        var dark = std(colors.deep.clone().lerp(colors.wall, 0.4), 0.6);
        add(new THREE.BoxGeometry(3.2, 0.42, 2.3), dark, 0.6, 0.21, -2.6);
        add(new THREE.BoxGeometry(3.4, 1.3, 0.16), dark, 0.6, 0.65, -3.82);
        add(new THREE.BoxGeometry(3.05, 0.28, 2.15), linen, 0.6, 0.56, -2.6);
        var pillowGeo = new THREE.SphereGeometry(0.5, 24, 16);
        [-0.1, 1.3].forEach(function (x) { var p = add(pillowGeo, linen, x, 0.82, -3.45); p.scale.set(1.25, 0.32, 0.55); });
        add(new THREE.BoxGeometry(3.1, 0.06, 0.95), std(colors.brass, 0.75, 0.1), 0.6, 0.72, -1.95);
        add(new THREE.CylinderGeometry(0.42, 0.42, 0.62, 32), dark, -1.75, 0.31, -3.1);
        add(new THREE.CylinderGeometry(0.14, 0.11, 0.3, 24), std(colors.brass, 0.4, 0.6), -1.75, 0.77, -3.1);
        var leaf = std(colors.deep.clone().lerp(new THREE.Color("#3f5a3a"), 0.75), 0.8);
        for (var i = 0; i < 7; i++) {
            var l = add(new THREE.SphereGeometry(0.18, 12, 10), leaf, -1.75 + Math.cos(i * 0.9) * 0.16, 1.05 + i * 0.07, -3.1 + Math.sin(i * 0.9) * 0.16);
            l.scale.set(0.5, 1.6, 0.3); l.rotation.z = Math.cos(i * 1.7) * 0.6;
        }
        add(new THREE.CircleGeometry(2.6, 64), std(colors.wall.clone().lerp(colors.brass, 0.18), 1), 0.6, 0.012, -1.9).rotation.x = -Math.PI / 2;

        // Pendant lamp: cord, brass shade, glowing bulb with a warm point light.
        add(new THREE.CylinderGeometry(0.012, 0.012, 3.0, 6), std(colors.deep, 0.5), -1.75, 4.5, -2.6);
        var shade = add(new THREE.ConeGeometry(0.42, 0.5, 40, 1, true), new THREE.MeshStandardMaterial({ color: colors.brass, roughness: 0.3, metalness: 0.85, side: THREE.DoubleSide }), -1.75, 2.85, -2.6);
        var bulbMat = new THREE.MeshBasicMaterial({ color: colors.brass2 });
        add(new THREE.SphereGeometry(0.11, 24, 16), bulbMat, -1.75, 2.62, -2.6);
        var lamp = new THREE.PointLight(colors.brass2, 6, 14, 1.6);
        lamp.position.set(-1.75, 2.5, -2.6);
        scene.add(lamp);
        var moonLight = new THREE.DirectionalLight(colors.skyLow.clone().lerp(new THREE.Color("#c9d6ff"), 0.5), 0.9);
        moonLight.position.set(1.5, 4, -10);
        scene.add(moonLight);
        scene.add(new THREE.HemisphereLight(colors.skyLow, colors.floor, 0.35));

        // Dust in the lamp light.
        var count = 420, pos = new Float32Array(count * 3);
        for (var d = 0; d < count; d++) { pos[d * 3] = -4 + Math.random() * 9; pos[d * 3 + 1] = Math.random() * 5; pos[d * 3 + 2] = -4 + Math.random() * 9; }
        var dustGeo = new THREE.BufferGeometry();
        dustGeo.setAttribute("position", new THREE.BufferAttribute(pos, 3));
        var dust = new THREE.Points(dustGeo, new THREE.PointsMaterial({ color: colors.brass2, size: 0.03, transparent: true, opacity: 0.7, depthWrite: false, blending: THREE.AdditiveBlending }));
        scene.add(dust);

        // Camera path: outside the room → through the arch, looking at the bed and lamp.
        var from = { x: 0.3, y: 2.2, z: 10.5, tx: -0.2, ty: 1.7, tz: -3.5 };
        var to = { x: 0.1, y: 1.7, z: 3.4, tx: 0.2, ty: 1.15, tz: -3.4 };
        var state = { p: 0 }, pointer = { x: 0, y: 0 }, eased = { x: 0, y: 0 };
        var target = new THREE.Vector3();
        function placeCamera() {
            var p = state.p, k = p * p * (3 - 2 * p);
            camera.position.set(from.x + (to.x - from.x) * k + eased.x * 0.6, from.y + (to.y - from.y) * k + eased.y * 0.3, from.z + (to.z - from.z) * k);
            target.set(from.tx + (to.tx - from.tx) * k + eased.x * 0.25, from.ty + (to.ty - from.ty) * k, from.tz + (to.tz - from.tz) * k);
            camera.lookAt(target);
            lamp.intensity = 6 + 16 * k;
            bulbMat.color.copy(colors.brass2).multiplyScalar(1 + k * 0.6);
        }

        function resize() {
            var w = hero.clientWidth, h = hero.clientHeight;
            renderer.setSize(w, h, false);
            camera.aspect = w / Math.max(h, 1);
            // Keep the room right of the text column on wide screens.
            camera.setViewOffset(w * 1.16, h, -w * 0.12, 0, w, h);
            camera.updateProjectionMatrix();
        }
        resize();
        window.addEventListener("resize", resize, { passive: true });
        hero.addEventListener("pointermove", function (e) {
            var r = hero.getBoundingClientRect();
            pointer.x = (e.clientX - r.left) / r.width - 0.5;
            pointer.y = -((e.clientY - r.top) / r.height - 0.5);
        }, { passive: true });

        if (gsap && ST) {
            var copy = hero.querySelector("[data-t3-copy]");
            var tl = gsap.timeline({ scrollTrigger: { trigger: hero, start: "top top", end: "+=110%", pin: true, scrub: 0.9, anticipatePin: 1, refreshPriority: 10 } });
            tl.to(state, { p: 1, ease: "none", duration: 1 }, 0);
            if (copy) tl.to(copy, { opacity: 0, y: -60, ease: "power1.in", duration: 0.45 }, 0);
        }

        var visible = true, running = false, last = 0, time = 0;
        function frame(now) {
            if (!visible || document.hidden) { running = false; return; }
            var dt = Math.min((now - (last || now)) / 1000, 0.05);
            last = now; time += dt;
            eased.x += (pointer.x - eased.x) * 0.04;
            eased.y += (pointer.y - eased.y) * 0.04;
            placeCamera();
            dust.rotation.y = time * 0.02;
            dust.position.y = Math.sin(time * 0.3) * 0.08;
            halo.material.opacity = 0.07 + Math.sin(time * 0.8) * 0.02;
            shade.rotation.z = Math.sin(time * 0.6) * 0.015;
            renderer.render(scene, camera);
            requestAnimationFrame(frame);
        }
        function resume() {
            if (running || !visible || document.hidden) return;
            running = true; last = 0;
            requestAnimationFrame(frame);
        }
        if ("IntersectionObserver" in window) {
            new IntersectionObserver(function (entries) { visible = entries[0].isIntersecting; resume(); }).observe(hero);
        }
        document.addEventListener("visibilitychange", resume);
        placeCamera();
        renderer.render(scene, camera);
        hero.classList.add("is-3d");
        if (ST) ST.refresh();
        resume();
        void moon;
    }

    function init() {
        if (reduceMotion || !gsap || !ST) return;
        gsap.registerPlugin(ST);
        drawings();
        heroText();
        storyMotion();
        rooms();
        room3d();
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
