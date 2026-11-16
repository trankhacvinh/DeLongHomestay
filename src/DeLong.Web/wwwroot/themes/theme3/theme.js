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
    var SUITE_URL = "/themes/theme3/room.js";

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
            import(SUITE_URL).then(function (mod) { buildRoom(mod, hero, canvas); }).catch(function () { /* line drawing stays */ });
        };
        if ("requestIdleCallback" in window) window.requestIdleCallback(start, { timeout: 1200 });
        else window.setTimeout(start, 200);
    }

    function buildRoom(suiteModule, hero, canvas) {
        var c = function (name, fallback) { return new suiteModule.THREE.Color(cssVar(hero, name, fallback)); };
        var colors = {
            deep: c("--t3-deep", "#17110e"), wall: c("--t3-wall", "#4a382d"), floor: c("--t3-floor", "#2b1f19"),
            brass: c("--t3-brass", "#c9a46a"), brass2: c("--t3-brass-2", "#e3c894"), ivory: c("--t3-ivory", "#f3ece2"),
            skyTop: c("--t3-sky-top", "#0f1426"), skyLow: c("--t3-sky-low", "#3a3550")
        };
        var suite = suiteModule.buildSuite(canvas, colors);
        var state = { p: 0 };
        function resize() { suite.setSize(hero.clientWidth, hero.clientHeight); }
        resize();
        window.addEventListener("resize", resize, { passive: true });
        hero.addEventListener("pointermove", function (e) {
            var r = hero.getBoundingClientRect();
            suite.setPointer((e.clientX - r.left) / r.width - 0.5, -((e.clientY - r.top) / r.height - 0.5));
        }, { passive: true });

        var copy = hero.querySelector("[data-t3-copy]");
        var tl = gsap.timeline({ scrollTrigger: { trigger: hero, start: "top top", end: "+=110%", pin: true, scrub: 0.9, anticipatePin: 1, refreshPriority: 10 } });
        tl.to(state, { p: 1, ease: "none", duration: 1, onUpdate: function () { suite.setProgress(state.p); } }, 0);
        if (copy) tl.to(copy, { opacity: 0, y: -60, ease: "power1.in", duration: 0.45 }, 0);

        var visible = true, running = false, last = 0;
        function frame(now) {
            if (!visible || document.hidden) { running = false; return; }
            var dt = Math.min((now - (last || now)) / 1000, 0.05);
            last = now;
            suite.render(dt);
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
        suite.render(0);
        hero.classList.add("is-3d");
        ST.refresh();
        resume();
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
