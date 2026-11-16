/*
 * Theme 2 · Chill. Three small things:
 *  1. one GSAP entrance for the hero copy (skipped with reduced motion);
 *  2. a slow dusk sky drawn with three.js behind the hero, desktop only, loaded on demand, paused off screen;
 *  3. prev/next buttons for the rooms rail.
 * Nothing here touches booking: the calendar, booking modal and payment scripts are shared by every theme.
 */
(function () {
    "use strict";
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;

    function heroEntrance() {
        var items = document.querySelectorAll(".tc-hero [data-tc-reveal]");
        if (!items.length || reduceMotion || !window.gsap) return;
        window.gsap.from(items, { opacity: 0, y: 36, duration: 1.1, ease: "expo.out", stagger: 0.12, delay: 0.1, clearProps: "opacity,transform" });
    }

    function canDrawSky() {
        if (reduceMotion) return false;
        if (!window.matchMedia("(min-width: 900px)").matches) return false;
        var conn = navigator.connection;
        if (conn && (conn.saveData || /(^|-)2g$/.test(conn.effectiveType || ""))) return false;
        try {
            var probe = document.createElement("canvas");
            return !!(probe.getContext("webgl2") || probe.getContext("webgl"));
        } catch (e) { return false; }
    }

    function cssColor(style, name, fallback) {
        var value = (style.getPropertyValue(name) || "").trim();
        return value || fallback;
    }

    var FRAGMENT = [
        "precision highp float;",
        "uniform float uTime; uniform vec2 uRes; uniform vec3 uTop; uniform vec3 uMid; uniform vec3 uLow;",
        "varying vec2 vUv;",
        "float hash(vec2 p){ p = fract(p * vec2(123.34, 456.21)); p += dot(p, p + 45.32); return fract(p.x * p.y); }",
        "float noise(vec2 p){ vec2 i = floor(p); vec2 f = fract(p); vec2 u = f * f * (3.0 - 2.0 * f);",
        "  return mix(mix(hash(i), hash(i + vec2(1.0, 0.0)), u.x), mix(hash(i + vec2(0.0, 1.0)), hash(i + vec2(1.0, 1.0)), u.x), u.y); }",
        "float fbm(vec2 p){ float v = 0.0; float a = 0.5; for (int i = 0; i < 5; i++){ v += a * noise(p); p *= 2.02; a *= 0.5; } return v; }",
        "void main(){",
        "  vec2 uv = vUv; float aspect = uRes.x / max(uRes.y, 1.0);",
        "  float t = uTime * 0.018;",
        "  float h = smoothstep(0.0, 1.0, uv.y);",
        "  vec3 sky = mix(uLow, uMid, smoothstep(0.0, 0.55, h));",
        "  sky = mix(sky, uTop, smoothstep(0.45, 1.0, h));",
        "  vec2 sunPos = vec2(0.74, 0.04);",
        "  float d = length((uv - sunPos) * vec2(aspect, 1.0));",
        "  sky += uLow * 0.3 * exp(-d * 2.8);",
        "  vec2 q = vec2(uv.x * aspect * 1.6 + t, uv.y * 3.2);",
        "  float clouds = fbm(q + fbm(q * 0.7 - t * 0.6));",
        "  float band = smoothstep(0.15, 0.55, uv.y) * (1.0 - smoothstep(0.55, 0.95, uv.y));",
        "  sky = mix(sky, mix(uMid, uLow, 0.35 + 0.4 * (1.0 - uv.y)), smoothstep(0.5, 0.85, clouds) * band * 0.55);",
        "  vec2 sg = uv * vec2(aspect, 1.0) * 90.0; vec2 sp = floor(sg); vec2 sf = fract(sg) - 0.5;",
        "  vec2 so = vec2(hash(sp + 3.1), hash(sp + 9.7)) - 0.5;",
        "  float star = step(0.985, hash(sp)) * smoothstep(0.08, 0.0, length(sf - so * 0.6)) * smoothstep(0.5, 1.0, uv.y);",
        "  star *= 0.35 + 0.35 * sin(uTime * 1.1 + hash(sp + 7.0) * 40.0);",
        "  sky += vec3(star);",
        "  sky *= 1.0 - 0.18 * smoothstep(0.35, 0.0, uv.y) * (1.0 - smoothstep(0.0, 0.6, abs(uv.x - sunPos.x)));",
        "  gl_FragColor = vec4(sky, 1.0);",
        "}"
    ].join("\n");

    function drawSky() {
        var hero = document.querySelector("[data-tc-hero]");
        var canvas = hero && hero.querySelector("[data-tc-dusk]");
        if (!canvas || !canDrawSky()) return;
        var start = function () {
            import("/vendor/three/0.160.1/three.module.min.js").then(function (THREE) {
                // Colours go to the shader exactly as written in CSS (no sRGB→linear step), so the sky matches the CSS fallback.
                if (THREE.ColorManagement) THREE.ColorManagement.enabled = false;
                var style = getComputedStyle(hero);
                var renderer = new THREE.WebGLRenderer({ canvas: canvas, antialias: false, alpha: false, powerPreference: "low-power" });
                renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
                var scene = new THREE.Scene();
                var camera = new THREE.OrthographicCamera(-1, 1, 1, -1, 0, 1);
                var uniforms = {
                    uTime: { value: 0 },
                    uRes: { value: new THREE.Vector2(1, 1) },
                    uTop: { value: new THREE.Color(cssColor(style, "--tc-sky-1", "#15112b")) },
                    uMid: { value: new THREE.Color(cssColor(style, "--tc-sky-2", "#3a2f74")) },
                    uLow: { value: new THREE.Color(cssColor(style, "--tc-sky-3", "#f08f7a")) }
                };
                var material = new THREE.ShaderMaterial({
                    uniforms: uniforms,
                    vertexShader: "varying vec2 vUv; void main(){ vUv = uv; gl_Position = vec4(position.xy, 0.0, 1.0); }",
                    fragmentShader: FRAGMENT,
                    depthTest: false,
                    depthWrite: false
                });
                scene.add(new THREE.Mesh(new THREE.PlaneGeometry(2, 2), material));

                function resize() {
                    var w = hero.clientWidth, h = hero.clientHeight;
                    renderer.setSize(w, h, false);
                    uniforms.uRes.value.set(w, h);
                }
                resize();
                window.addEventListener("resize", resize, { passive: true });

                var visible = true, running = false, last = 0, elapsed = 0;
                function frame(now) {
                    if (!visible || document.hidden) { running = false; return; }
                    elapsed += Math.min((now - (last || now)) / 1000, 0.1);
                    last = now;
                    uniforms.uTime.value = elapsed;
                    renderer.render(scene, camera);
                    requestAnimationFrame(frame);
                }
                function resume() {
                    if (running || !visible || document.hidden) return;
                    running = true; last = 0;
                    requestAnimationFrame(frame);
                }
                if ("IntersectionObserver" in window) {
                    new IntersectionObserver(function (entries) {
                        visible = entries[0].isIntersecting;
                        resume();
                    }).observe(hero);
                }
                document.addEventListener("visibilitychange", resume);
                renderer.render(scene, camera);
                hero.classList.add("is-live");
                resume();
            }).catch(function () { /* the CSS gradient stays */ });
        };
        if ("requestIdleCallback" in window) window.requestIdleCallback(start, { timeout: 1500 });
        else window.setTimeout(start, 300);
    }

    function rails() {
        document.querySelectorAll("[data-tc-rail]").forEach(function (rail) {
            var section = rail.closest("section");
            var prev = section && section.querySelector("[data-tc-rail-prev]");
            var next = section && section.querySelector("[data-tc-rail-next]");
            if (!prev || !next) return;
            function step() {
                var card = rail.querySelector("li");
                return card ? card.getBoundingClientRect().width + 22 : rail.clientWidth * 0.8;
            }
            function update() {
                prev.disabled = rail.scrollLeft <= 2;
                next.disabled = rail.scrollLeft + rail.clientWidth >= rail.scrollWidth - 2;
            }
            prev.addEventListener("click", function () { rail.scrollBy({ left: -step(), behavior: reduceMotion ? "auto" : "smooth" }); });
            next.addEventListener("click", function () { rail.scrollBy({ left: step(), behavior: reduceMotion ? "auto" : "smooth" }); });
            rail.addEventListener("scroll", update, { passive: true });
            window.addEventListener("resize", update, { passive: true });
            update();
        });
    }

    function init() { heroEntrance(); drawSky(); rails(); }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
