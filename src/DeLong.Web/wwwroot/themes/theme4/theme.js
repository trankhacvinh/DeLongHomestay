/*
 * Theme 4 · Ambient. Slow, quiet motion only:
 *  1. a greeting that matches the visitor's local time (the sky colours are switched by an inline script in the hero);
 *  2. one soft GSAP entrance for the hero text;
 *  3. scroll parallax of the landscape layers (GSAP ScrollTrigger) and a slight drift with the pointer on desktop;
 *  4. fireflies on a small 2D canvas at dusk/night and in the closing band, paused off screen.
 * Reduced motion: none of it runs. Booking scripts are shared by every theme and untouched.
 */
(function () {
    "use strict";
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var gsap = window.gsap;

    function greeting() {
        var el = document.querySelector("[data-t4-greeting]");
        if (!el) return;
        var h = new Date().getHours();
        el.textContent = h >= 5 && h < 11 ? "Chào buổi sáng." : h >= 11 && h < 13 ? "Chào buổi trưa." : h >= 13 && h < 18 ? "Chào buổi chiều." : "Chào buổi tối.";
    }

    function entrance(hero) {
        var lines = hero.querySelectorAll("[data-t4-line]");
        var scene = hero.querySelector(".t4-scene");
        var tl = gsap.timeline({ defaults: { ease: "sine.out" } });
        if (scene) tl.from(scene, { scale: 1.05, transformOrigin: "50% 100%", duration: 3.2 }, 0);
        tl.from(lines, { opacity: 0, y: 18, filter: "blur(6px)", duration: 1.6, stagger: 0.16, clearProps: "filter,opacity,transform" }, 0.2);
    }

    function parallax(hero) {
        var layers = Array.prototype.slice.call(hero.querySelectorAll(".t4-layer"));
        var copy = hero.querySelector("[data-t4-copy]");
        if (window.ScrollTrigger) {
            gsap.registerPlugin(window.ScrollTrigger);
            var trigger = { trigger: hero, start: "top top", end: "bottom top", scrub: 0.6 };
            layers.forEach(function (layer) {
                var depth = parseFloat(layer.getAttribute("data-depth") || "0.2");
                gsap.to(layer, { y: depth * 420, ease: "none", scrollTrigger: trigger });
            });
            if (copy) gsap.to(copy, { y: 90, opacity: 0.15, ease: "none", scrollTrigger: trigger });
        }
        if (!window.matchMedia("(hover: hover) and (pointer: fine)").matches) return;
        var movers = layers.map(function (layer) {
            return { depth: parseFloat(layer.getAttribute("data-depth") || "0.2"), x: gsap.quickTo(layer, "x", { duration: 2.4, ease: "sine.out" }) };
        });
        hero.addEventListener("pointermove", function (e) {
            var r = hero.getBoundingClientRect();
            var dx = (e.clientX - r.left) / r.width - 0.5;
            movers.forEach(function (m) { m.x(-dx * m.depth * 90); });
        }, { passive: true });
    }

    function cssVar(el, name, fallback) {
        var v = getComputedStyle(el).getPropertyValue(name).trim();
        return v || fallback;
    }

    function fireflies(canvas) {
        var host = canvas.parentNode;
        if (host.hasAttribute("data-t4-hero")) {
            var time = host.getAttribute("data-t4-time");
            if (time !== "dusk" && time !== "night") return;
        }
        var ctx = canvas.getContext("2d");
        if (!ctx) return;
        var color = cssVar(host, "--t4-lantern", "#e9b872");
        var dpr = Math.min(window.devicePixelRatio || 1, 2);
        var w = 0, h = 0, flies = [];
        var sprite = document.createElement("canvas");
        sprite.width = sprite.height = 32;
        var s = sprite.getContext("2d");
        var g = s.createRadialGradient(16, 16, 0, 16, 16, 16);
        g.addColorStop(0, "#fff");
        g.addColorStop(0.18, color);
        g.addColorStop(1, "rgba(0,0,0,0)");
        s.fillStyle = g;
        s.fillRect(0, 0, 32, 32);

        function resize() {
            w = host.clientWidth; h = host.clientHeight;
            canvas.width = Math.round(w * dpr); canvas.height = Math.round(h * dpr);
            ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
            var count = Math.round(Math.min(34, Math.max(12, w / 42)));
            flies = [];
            for (var i = 0; i < count; i++) {
                flies.push({
                    x: Math.random() * w,
                    y: h * (0.35 + Math.random() * 0.6),
                    size: 10 + Math.random() * 12,
                    phase: Math.random() * Math.PI * 2,
                    speed: 0.15 + Math.random() * 0.35,
                    drift: (Math.random() - 0.5) * 0.25
                });
            }
        }
        resize();
        window.addEventListener("resize", resize, { passive: true });

        var visible = true, running = false, last = 0;
        function frame(now) {
            if (!visible || document.hidden) { running = false; return; }
            var dt = Math.min((now - (last || now)) / 16.7, 3);
            last = now;
            ctx.clearRect(0, 0, w, h);
            for (var i = 0; i < flies.length; i++) {
                var f = flies[i];
                f.phase += 0.012 * dt;
                f.x += (Math.sin(f.phase * 0.7) * 0.3 + f.drift) * f.speed * dt;
                f.y += Math.cos(f.phase) * 0.22 * f.speed * dt;
                if (f.x < -20) f.x = w + 20; else if (f.x > w + 20) f.x = -20;
                ctx.globalAlpha = 0.25 + 0.75 * Math.pow((Math.sin(f.phase * 1.9) + 1) / 2, 3);
                ctx.drawImage(sprite, f.x - f.size / 2, f.y - f.size / 2, f.size, f.size);
            }
            requestAnimationFrame(frame);
        }
        function resume() {
            if (running || !visible || document.hidden) return;
            running = true; last = 0;
            requestAnimationFrame(frame);
        }
        if ("IntersectionObserver" in window) {
            new IntersectionObserver(function (entries) { visible = entries[0].isIntersecting; resume(); }).observe(host);
        }
        document.addEventListener("visibilitychange", resume);
        resume();
    }

    function init() {
        greeting();
        if (reduceMotion) return;
        var hero = document.querySelector("[data-t4-hero]");
        if (hero && gsap) { entrance(hero); parallax(hero); }
        document.querySelectorAll("[data-t4-fireflies]").forEach(fireflies);
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init);
    else init();
})();
