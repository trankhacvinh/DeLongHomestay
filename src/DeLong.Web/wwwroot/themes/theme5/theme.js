/*
 * Theme 5 · Pixel. Three small engines, no libraries:
 *  1. the hero world: a tiny canvas (about 4 screen pixels per pixel) with a dithered sky and five parallax layers
 *     (far peaks, hills with pines, the homestay, a path, foreground grass). A guest walks, the camera drifts,
 *     the pointer tilts the layers with a spring and scrolling separates them by depth;
 *  2. blob morphing: low-resolution metaballs behind the highlights and the closing band, quantised to the palette,
 *     one blob follows the pointer with inertia;
 *  3. the rooms rail: drag or fling it, it keeps its momentum, rubber-bands at the ends and settles on a card.
 * Everything pauses off screen. Reduced motion: one still frame, no momentum. Booking scripts are untouched.
 */
(function () {
    "use strict";
    var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
    var finePointer = window.matchMedia && window.matchMedia("(hover: hover) and (pointer: fine)").matches;
    var BAYER = [0, 8, 2, 10, 12, 4, 14, 6, 3, 11, 1, 9, 15, 7, 13, 5];

    function cssVar(name, fallback) {
        var v = getComputedStyle(document.body).getPropertyValue(name).trim();
        return v || fallback;
    }
    function rgb(hex) {
        var h = (hex || "#000").replace("#", "");
        if (h.length === 3) h = h[0] + h[0] + h[1] + h[1] + h[2] + h[2];
        var n = parseInt(h.slice(0, 6), 16);
        return isNaN(n) ? [0, 0, 0] : [(n >> 16) & 255, (n >> 8) & 255, n & 255];
    }
    function mix(a, b, t) { return [Math.round(a[0] + (b[0] - a[0]) * t), Math.round(a[1] + (b[1] - a[1]) * t), Math.round(a[2] + (b[2] - a[2]) * t)]; }
    function css(c) { return "rgb(" + c[0] + "," + c[1] + "," + c[2] + ")"; }
    function luma(c) { return (0.2126 * c[0] + 0.7152 * c[1] + 0.0722 * c[2]) / 255; }
    function hash(n) { var x = Math.sin(n * 127.1 + 311.7) * 43758.5453; return x - Math.floor(x); }

    /* Runs fn(dt) on animation frames while `el` is on screen and the tab is visible; `fps` caps the rate. */
    function loop(el, fps, fn) {
        var visible = false, raf = 0, last = 0, step = 1000 / fps;
        function frame(t) {
            raf = 0;
            if (!visible || document.hidden) return;
            raf = requestAnimationFrame(frame);
            if (last && t - last < step - 2) return;
            var dt = last ? Math.min(t - last, 100) : step;
            last = t;
            fn(dt / 1000, t / 1000);
        }
        function start() { if (!raf && visible && !document.hidden) { last = 0; raf = requestAnimationFrame(frame); } }
        new IntersectionObserver(function (entries) { visible = entries[0].isIntersecting; start(); }, { rootMargin: "80px" }).observe(el);
        document.addEventListener("visibilitychange", start);
    }

    /* ---------------------------------------------------------------- hero world */
    function world(canvas) {
        var hero = canvas.closest("[data-t5-hero]") || canvas.parentNode;
        var ctx = canvas.getContext("2d");
        if (!ctx) return;
        var W = 0, H = 0, px = 4, ground = 0, sky = null, colors = null;
        var cam = 0, tiltX = 0, tiltY = 0, velX = 0, velY = 0, aimX = 0, aimY = 0, lift = 0;
        var walkT = 0, smoke = [], flies = [], reveal = 0;
        var dir = 1, speed = 1, face = 1, jumpY = 0, jumpV = 0;   // driven by the controller's cross pad
        var screen = canvas.parentNode;
        var small = document.createElement("canvas"), sctx = small.getContext("2d");

        function readColors() {
            var c = {};
            ["sky-1", "sky-2", "sky-3", "sky-4", "far", "mid", "near", "grass", "wall", "roof", "light", "ink", "go", "gold", "mint", "paper"].forEach(function (k) {
                c[k] = rgb(cssVar("--t5-" + k, "#888888"));
            });
            c.night = luma(c["sky-3"]) < 0.35;
            return c;
        }

        function size() {
            // The world lives inside the TV screen: about 150 world pixels tall whatever the screen size.
            var r = screen.getBoundingClientRect();
            px = Math.max(1.5, r.height / 150);
            W = Math.max(120, Math.ceil(r.width / px));
            H = Math.max(90, Math.ceil(r.height / px));
            canvas.width = W; canvas.height = H;
            ground = H - Math.max(18, Math.round(H * 0.2));
            colors = readColors();
            paintSky();
            flies = [];
            for (var i = 0; i < 14; i++) flies.push({ x: hash(i) * W, y: ground - 4 - hash(i + 40) * H * 0.18, p: hash(i + 9) * 6.28 });
        }

        /* The sky never moves, so it is dithered once into ImageData: four bands, ordered (Bayer) dithering between them, a sun or moon and stars. */
        function paintSky() {
            var img = ctx.createImageData(W, H), d = img.data, stops = [colors["sky-1"], colors["sky-2"], colors["sky-3"], colors["sky-4"]];
            var horizon = ground - 6, sunX = Math.round(W * 0.74), sunY = Math.round(horizon * 0.62), sunR = Math.max(8, Math.round(H * 0.075));
            for (var y = 0; y < H; y++) {
                var t = Math.min(0.999, Math.max(0, y / horizon)) * 3, band = Math.floor(t), f = t - band;
                for (var x = 0; x < W; x++) {
                    var c = stops[Math.min(3, band + (f * 16 > BAYER[(y & 3) * 4 + (x & 3)] + 0.5 ? 1 : 0))];
                    var dx = x - sunX, dy = y - sunY, dist = Math.sqrt(dx * dx + dy * dy);
                    // Sun: a disc with the classic retro gaps in its lower half. Night: a moon with a few craters.
                    if (dist < sunR) {
                        if (colors.night) c = (hash(Math.floor(x / 3) * 7 + Math.floor(y / 3)) > 0.86) ? mix(colors.light, c, 0.35) : mix(colors.light, [255, 255, 255], 0.45);
                        else c = y > sunY && (y - sunY) % 4 === 0 && (y - sunY) / sunR * 4 > 1 ? c : colors.light;
                    }
                    var o = (y * W + x) * 4;
                    d[o] = c[0]; d[o + 1] = c[1]; d[o + 2] = c[2]; d[o + 3] = 255;
                }
            }
            var stars = luma(colors["sky-1"]) < 0.45 ? Math.round(W * H / 900) : 0;
            for (var s = 0; s < stars; s++) {
                var sx = Math.floor(hash(s * 3.1) * W), sy = Math.floor(hash(s * 7.7) * horizon * 0.55), so = (sy * W + sx) * 4;
                d[so] = d[so + 1] = d[so + 2] = 255; d[so + 3] = 255;
            }
            sky = img;
        }

        function height(x, seed, base, amp) {
            return base - amp * (0.55 * Math.sin(x * 0.021 + seed) + 0.3 * Math.sin(x * 0.053 + seed * 2.3) + 0.15 * Math.sin(x * 0.13 + seed * 0.7));
        }

        /* A filled heightmap layer; `depth` 0 = horizon, 1 = foreground. */
        function ridge(offset, dy, seed, base, amp, color, pines, pineColor) {
            ctx.fillStyle = css(color);
            for (var x = 0; x < W; x++) {
                var wx = x + offset, top = Math.round(height(wx, seed, base, amp) + dy);
                ctx.fillRect(x, top, 1, H - top);
            }
            if (!pines) return;
            ctx.fillStyle = css(pineColor);
            var first = Math.floor(offset / 9) * 9;
            for (var p = first; p < offset + W + 9; p += 9) {
                if (hash(p) > 0.45) continue;
                var tall = 6 + Math.floor(hash(p + 1) * 7), sx = Math.round(p - offset), base2 = Math.round(height(p, seed, base, amp) + dy) + 2;
                for (var r = 0; r < tall; r++) {
                    var half = Math.floor((r % 4 + r / 2) / 2);
                    ctx.fillRect(sx - half, base2 - tall + r, half * 2 + 1, 1);
                }
                ctx.fillRect(sx, base2, 1, 2);
            }
        }

        function rect(x, y, w, h, c) { ctx.fillStyle = typeof c === "string" ? c : css(c); ctx.fillRect(Math.round(x), Math.round(y), w, h); }

        /* The homestay: stepped roof, two lit windows, a door, a chimney and a lamp post. */
        function house(x, base, t) {
            var c = colors, wall = c.wall, roof = c.roof, dark = mix(c.ink, c.near, 0.3), roofDark = mix(roof, c.ink, 0.35);
            rect(x + 26, base - 34, 4, 9, mix(roof, c.ink, 0.5));          // chimney
            for (var r = 0; r < 12; r++) {                                  // stepped roof, apex over the door
                var half = 3 + Math.floor(r * 1.7);
                rect(x + 17 - half, base - 37 + r, half * 2 + 1, 1, r < 2 || r % 3 === 2 ? roofDark : roof);
            }
            rect(x - 3, base - 26, 40, 2, roofDark);
            rect(x, base - 24, 34, 24, wall);
            rect(x, base - 24, 34, 2, mix(wall, c.ink, 0.25));
            var flicker = reduceMotion ? 1 : 0.85 + 0.15 * Math.sin(t * 7 + Math.sin(t * 13));
            [[4, 9], [24, 9]].forEach(function (w) {
                rect(x + w[0] - 1, base - 20, 8, 9, dark);
                rect(x + w[0], base - 19, 6, 7, mix(c.light, [255, 255, 255], 0.1 * flicker));
                rect(x + w[0] + 2, base - 19, 1, 7, mix(c.light, c.go, 0.4));
                rect(x + w[0], base - 16, 6, 1, mix(c.light, c.go, 0.4));
            });
            rect(x + 14, base - 13, 7, 13, dark);
            rect(x + 15, base - 12, 5, 12, mix(c.roof, c.ink, 0.25));
            rect(x + 18, base - 6, 1, 1, c.gold);
            rect(x + 44, base - 16, 1, 16, dark);                             // lamp post
            rect(x + 42, base - 18, 5, 3, c.light);
            if ((Math.floor(t * 2) & 1) === 0 || reduceMotion) rect(x + 41, base - 1, 7, 1, mix(c.light, c.near, 0.5));
        }

        /* Guest sprite, 7×12, two walking frames. */
        function guest(x0, base, frame) {
            var x = x0;
            // Mirror the sprite when walking left: draw through a rect() that flips around the sprite's 7px box.
            var put = face > 0 ? rect : function (rx, ry, w, h, col) { rect(x0 + 7 - (rx - x0) - w, ry, w, h, col); };
            var c = colors, skin = [245, 197, 160], hair = mix(c.ink, [80, 50, 30], 0.4), shirt = c.go, pants = c.ink, bob = frame ? 0 : 1;
            var y = base - 12 + bob;
            put(x + 2, y, 3, 1, hair);
            put(x + 1, y + 1, 5, 3, skin); put(x + 1, y + 1, 5, 1, hair); put(x + 4, y + 2, 1, 1, c.ink);
            put(x + 1, y + 4, 5, 4, shirt); put(x + 6, y + 5, 1, 2, skin);
            put(x, y + 4, 1, 3, mix(c.gold, c.ink, 0.3)); // backpack
            if (frame) { put(x + 1, y + 8, 2, 3 - bob, pants); put(x + 4, y + 8, 2, 3 - bob, pants); }
            else { put(x + 2, y + 8, 3, 2, pants); put(x + 1, y + 10, 2, 1, pants); put(x + 4, y + 10, 2, 1, pants); }
        }

        function draw(dt, t) {
            if (!W) return;
            var c = colors;
            ctx.putImageData(sky, 0, 0);

            // Clouds: chunky ovals drifting at their own speed.
            var cloud = mix(c["sky-4"], [255, 255, 255], c.night ? 0.05 : 0.45), shade = mix(cloud, c["sky-3"], 0.45);
            for (var k = 0; k < 4; k++) {
                var cw = 22 + Math.floor(hash(k + 3) * 26), span = W + cw * 2;
                var cx = Math.round(((hash(k) * span - cam * 0.06 - t * (1.2 + k * 0.4) + tiltX * 0.2) % span + span) % span - cw);
                var cy = Math.round(8 + hash(k + 20) * ground * 0.35 + lift * 0.9);
                ctx.globalAlpha = c.night ? 0.3 : 1;
                ctx.fillStyle = css(cloud);
                ctx.fillRect(cx + 4, cy, cw - 8, 2); ctx.fillRect(cx, cy + 2, cw, 2); ctx.fillRect(cx + 6, cy - 2, Math.floor(cw / 3), 2); ctx.fillRect(cx + Math.floor(cw / 2), cy - 3, Math.floor(cw / 4), 3);
                ctx.fillStyle = css(shade);
                ctx.fillRect(cx + 1, cy + 4, cw - 2, 1);
            }
            ctx.globalAlpha = 1;

            var far = mix(c.far, c["sky-3"], 0.25), mid = c.mid, near = c.near;
            ridge(cam * 0.12 + tiltX * 0.2, lift * 0.7 + tiltY * 0.2, 1.7, ground - H * 0.24, H * 0.13, far, false);
            ridge(cam * 0.3 + tiltX * 0.5, lift * 0.45 + tiltY * 0.5, 4.2, ground - H * 0.08, H * 0.07, mid, true, mix(mid, c.ink, 0.35));

            // Near ground: the plot of land the homestay sits on, repeated along the walk.
            var nearOff = cam * 0.62 + tiltX, ny = Math.round(lift * 0.2 + tiltY);
            rect(0, ground + ny, W, H - ground, near);
            rect(0, ground + ny, W, 1, mix(near, c.grass, 0.6));
            var period = Math.max(W * 0.9, 220), hx = ((W * 0.62 - nearOff) % period + period) % period - 60;
            house(hx, ground + ny, t);
            if (hx + period - 60 < W) house(hx + period, ground + ny, t);
            if (hx - period > -60) house(hx - period, ground + ny, t);

            // Chimney smoke rising from the house nearest the middle.
            if (!reduceMotion) {
                if (Math.random() < dt * 2.2) smoke.push({ x: hx + 27 + Math.random() * 2, y: ground + ny - 36, a: 0 });
                ctx.fillStyle = css(mix(c.wall, c["sky-2"], 0.35));
                smoke = smoke.filter(function (s) {
                    s.a += dt; s.y -= dt * 9; s.x += dt * (4 + Math.sin(s.a * 3) * 3);
                    var sz = 1 + Math.floor(s.a * 1.6);
                    ctx.globalAlpha = Math.max(0, 0.8 - s.a * 0.32);
                    ctx.fillRect(Math.round(s.x), Math.round(s.y), sz, sz);
                    return s.a < 2.5;
                });
                ctx.globalAlpha = 1;
            }

            // The path and the guest walking on it.
            var pathY = ground + ny + 4;
            rect(0, pathY, W, 2, mix(near, c.wall, 0.35));
            for (var d = -((Math.floor(nearOff) % 7) + 7) % 7; d < W; d += 7) rect(d, pathY + 2, 2, 1, mix(near, c.wall, 0.2));
            guest(Math.round(W * 0.56), pathY + 1 - Math.round(jumpY), dir === 0 && !jumpY ? 1 : Math.floor(walkT * 6 * speed) & 1);

            // Fireflies hover over the meadow.
            ctx.fillStyle = css(c.light);
            flies.forEach(function (f, i) {
                var on = Math.sin(t * 2 + f.p) > 0.2 || reduceMotion;
                if (!on) return;
                var fx = Math.round(((f.x - nearOff * 0.9 + Math.sin(t * 0.8 + i) * 6) % W + W) % W), fy = Math.round(f.y + Math.cos(t * 1.1 + f.p) * 3 + ny);
                ctx.globalAlpha = c.night ? 1 : 0.7;
                ctx.fillRect(fx, fy, 1, 1);
            });
            ctx.globalAlpha = 1;

            // Foreground grass, fastest layer.
            var fgOff = cam * 1.25 + tiltX * 1.8, fgY = H - Math.max(5, Math.round(H * 0.035)) + Math.round(tiltY * 1.5);
            ctx.fillStyle = css(mix(c.grass, c.ink, 0.15));
            ctx.fillRect(0, fgY + 3, W, H);
            for (var g = 0; g < W; g++) {
                var gx = Math.floor(g + fgOff), blade = Math.floor(hash(gx) * 4);
                if (blade) ctx.fillRect(g, fgY + 3 - blade, 1, blade);
            }
            ctx.fillStyle = css(c.grass);
            for (var g2 = 0; g2 < W; g2 += 1) {
                var gx2 = Math.floor(g2 + fgOff), b2 = hash(gx2 + 0.5) > 0.82 ? 2 : 0;
                if (b2) ctx.fillRect(g2, fgY + 2 - b2, 1, b2 + 1);
            }

            // Opening move: the world resolves from coarse blocks to full detail.
            if (reveal > 0.02) {
                var block = 1 + Math.round(reveal * reveal * 14);
                if (block > 1) {
                    var sw = Math.ceil(W / block), sh = Math.ceil(H / block);
                    small.width = sw; small.height = sh;
                    sctx.imageSmoothingEnabled = false; ctx.imageSmoothingEnabled = false;
                    sctx.drawImage(canvas, 0, 0, sw, sh);
                    ctx.drawImage(small, 0, 0, sw, sh, 0, 0, sw * block, sh * block);
                }
                reveal = Math.max(0, reveal - dt * 1.4);
            }
        }

        function step(dt, t) {
            if (dir !== 0) walkT += dt;
            cam += dt * 14 * dir * speed;
            if (jumpY > 0 || jumpV > 0) { jumpV -= 260 * dt; jumpY = Math.max(0, jumpY + jumpV * dt); if (jumpY === 0) jumpV = 0; }
            // Spring the pointer tilt so layers overshoot slightly and settle: the "weight" of the world.
            velX += ((aimX - tiltX) * 38 - velX * 7.5) * dt; tiltX += velX * dt;
            velY += ((aimY - tiltY) * 38 - velY * 7.5) * dt; tiltY += velY * dt;
            var r = hero.getBoundingClientRect();
            lift = Math.max(0, -r.top) / px * 0.35;
            draw(dt, t);
        }

        size();
        var resizeTimer = 0;
        window.addEventListener("resize", function () {
            clearTimeout(resizeTimer);
            resizeTimer = setTimeout(function () { size(); if (reduceMotion) draw(0, 0); }, 120);
        });
        if (reduceMotion) { draw(0, 0); return; }
        if (finePointer) {
            hero.addEventListener("pointermove", function (e) {
                var r = hero.getBoundingClientRect();
                aimX = -((e.clientX - r.left) / r.width - 0.5) * 14;
                aimY = -((e.clientY - r.top) / r.height - 0.5) * 6;
            }, { passive: true });
            hero.addEventListener("pointerleave", function () { aimX = 0; aimY = 0; });
        }
        loop(hero, finePointer ? 60 : 30, step);

        // The cross pad: left and right walk (holding speeds up), down stops, up jumps. Arrow keys work while the hero is on screen.
        function press(d) {
            if (d === "up") { if (jumpY === 0) jumpV = 95; return; }
            if (d === "down") { dir = 0; return; }
            var nd = d === "left" ? -1 : 1;
            speed = dir === nd ? 2.2 : 1;
            dir = nd; face = nd;
        }
        function release() { speed = 1; }
        Array.prototype.forEach.call(hero.querySelectorAll("[data-t5-dir]"), function (btn) {
            var d = btn.getAttribute("data-t5-dir");
            btn.addEventListener("pointerdown", function () { press(d); blip(btn); });
            btn.addEventListener("pointerup", release);
            btn.addEventListener("pointerleave", release);
            btn.addEventListener("keydown", function (e) { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); press(d); blip(btn); } });
        });
        var heroVisible = false;
        new IntersectionObserver(function (en) { heroVisible = en[0].isIntersecting; }).observe(hero);
        document.addEventListener("keydown", function (e) {
            if (!heroVisible || e.altKey || e.ctrlKey || e.metaKey) return;
            var tag = (document.activeElement && document.activeElement.tagName) || "";
            if (/INPUT|TEXTAREA|SELECT/.test(tag) || (document.activeElement && document.activeElement.isContentEditable)) return;
            var map = { ArrowLeft: "left", ArrowRight: "right" }, d = map[e.key];
            if (!d) return;
            press(d);
            var btn = hero.querySelector('[data-t5-dir="' + d + '"]');
            if (btn) { btn.classList.add("is-pressed"); setTimeout(function () { btn.classList.remove("is-pressed"); }, 120); }
        });
        document.addEventListener("keyup", release);
    }

    /* ---------------------------------------------------------------- button feel: press, beep, coin */
    var audio = null;
    function beep(freq, len) {
        try {
            audio = audio || new (window.AudioContext || window.webkitAudioContext)();
            var o = audio.createOscillator(), g = audio.createGain(), now = audio.currentTime;
            o.type = "square"; o.frequency.setValueAtTime(freq, now); o.frequency.setValueAtTime(freq * 1.5, now + len * 0.45);
            g.gain.setValueAtTime(0.035, now); g.gain.exponentialRampToValueAtTime(0.0001, now + len);
            o.connect(g); g.connect(audio.destination); o.start(now); o.stop(now + len);
        } catch (err) { /* no audio: the press still works */ }
    }
    function blip(btn) {
        beep(440, 0.06);
        var tv = document.querySelector("[data-t5-tv]");
        if (tv && !reduceMotion) { tv.classList.remove("is-blip"); void tv.offsetWidth; tv.classList.add("is-blip"); }
    }
    function coin(x, y) {
        if (reduceMotion) return;
        var pop = document.createElement("span");
        pop.className = "t5-pop"; pop.textContent = "+1";
        pop.style.left = x + "px"; pop.style.top = (y - 20) + "px";
        document.body.appendChild(pop);
        setTimeout(function () { pop.remove(); }, 800);
    }
    function buttons() {
        // Every pixel button and the controller's A, B and Start keys: a coin pops out and a short beep plays.
        document.addEventListener("pointerdown", function (e) {
            var el = e.target.closest && e.target.closest('body[data-theme="theme5"] .t5-btn, [data-t5-press]');
            if (!el) return;
            coin(e.clientX, e.clientY);
            beep(el.matches(".t5-ab-a, .t5-btn-go, .t5-start") ? 660 : 520, 0.09);
        });
        // A, B and Start show their press before the page changes (a beat, not a delay you notice).
        Array.prototype.forEach.call(document.querySelectorAll("[data-t5-press]"), function (link) {
            link.addEventListener("click", function (e) {
                if (e.defaultPrevented || e.button !== 0 || e.metaKey || e.ctrlKey || e.shiftKey || e.altKey) return;
                var href = link.getAttribute("href") || "";
                e.preventDefault();
                link.classList.add("is-pressed");
                if (e.detail === 0) { var r = link.getBoundingClientRect(); coin(r.left + r.width / 2, r.top); beep(660, 0.09); }
                setTimeout(function () {
                    link.classList.remove("is-pressed");
                    if (href.charAt(0) === "#") {
                        var target = document.querySelector(href);
                        if (target) target.scrollIntoView({ behavior: reduceMotion ? "auto" : "smooth", block: "start" });
                    } else window.location.href = link.href;
                }, reduceMotion ? 0 : 140);
            });
        });
    }

    /* The TV switches on (a white line opening into the picture) the first time it comes into view. */
    function tvPower() {
        var tv = document.querySelector("[data-t5-tv]");
        if (!tv || reduceMotion) return;
        var io = new IntersectionObserver(function (en) {
            if (!en[0].isIntersecting) return;
            tv.classList.add("is-on"); io.disconnect();
        }, { threshold: 0.4 });
        io.observe(tv);
    }

    /* ---------------------------------------------------------------- blob morphing */
    function blobs(canvas) {
        var host = canvas.parentNode, ctx = canvas.getContext("2d");
        if (!ctx) return;
        var night = canvas.getAttribute("data-t5-blobs-mood") === "night";
        var W = 0, H = 0, img = null, palette = null, balls = [], cell = 10;
        var pointer = { x: 0.7, y: 0.4, vx: 0, vy: 0, aimX: 0.7, aimY: 0.4, active: false };

        function size() {
            var r = host.getBoundingClientRect();
            cell = r.width < 640 ? 8 : 10;
            W = Math.max(40, Math.ceil(r.width / cell)); H = Math.max(24, Math.ceil(r.height / cell));
            canvas.width = W; canvas.height = H;
            img = ctx.createImageData(W, H);
            var bg = rgb(cssVar(night ? "--t5-ink" : "--t5-paper-2", "#fff")), mint = rgb(cssVar("--t5-mint", "#4fc4a0")), gold = rgb(cssVar("--t5-gold", "#ffc145")), go = rgb(cssVar("--t5-go", "#ef6351"));
            palette = night
                ? [bg, mix(bg, go, 0.18), mix(bg, go, 0.32), mix(bg, mix(go, gold, 0.5), 0.5)]
                : [bg, mix(bg, mint, 0.14), mix(bg, mint, 0.26), mix(bg, mix(mint, gold, 0.3), 0.42)];
            var n = W < 70 ? 4 : 6;
            balls = [];
            for (var i = 0; i < n; i++) balls.push({ p: hash(i + (night ? 50 : 0)) * 6.28, s: 0.12 + hash(i + 7) * 0.14, r: 0.1 + hash(i + 3) * 0.08, ax: 0.2 + hash(i + 11) * 0.3, ay: 0.18 + hash(i + 13) * 0.25, cx: (i + 0.5) / n });
        }

        function draw(t) {
            if (!W) return;
            var d = img.data, aspect = W / H, list = [];
            balls.forEach(function (b) {
                list.push({ x: b.cx + Math.sin(t * b.s * 2 + b.p) * b.ax * 0.5, y: 0.5 + Math.cos(t * b.s * 1.6 + b.p * 1.3) * b.ay, r: b.r * (0.85 + 0.15 * Math.sin(t * 0.7 + b.p)) });
            });
            list.push({ x: pointer.x, y: pointer.y, r: pointer.active ? 0.16 : 0.1 });
            for (var y = 0; y < H; y++) {
                var ny = y / H;
                for (var x = 0; x < W; x++) {
                    var nx = x / W, f = 0;
                    for (var i = 0; i < list.length; i++) {
                        var b = list[i], dx = (nx - b.x) * aspect, dy = ny - b.y;
                        f += (b.r * b.r) / (dx * dx + dy * dy + 0.0004);
                    }
                    var lvl = f > 2.6 ? 3 : f > 1.5 ? 2 : f > 0.9 ? 1 : 0;
                    // Ordered dithering on the outer rim keeps the edge soft but still made of pixels.
                    if (lvl === 0 && f > 0.74 && (f - 0.74) / 0.16 * 16 > BAYER[(y & 3) * 4 + (x & 3)]) lvl = 1;
                    var c = palette[lvl], o = (y * W + x) * 4;
                    d[o] = c[0]; d[o + 1] = c[1]; d[o + 2] = c[2]; d[o + 3] = 255;
                }
            }
            ctx.putImageData(img, 0, 0);
        }

        size();
        var resizeTimer = 0;
        window.addEventListener("resize", function () { clearTimeout(resizeTimer); resizeTimer = setTimeout(function () { size(); draw(reduceMotion ? 3 : performance.now() / 1000); }, 120); });
        if (reduceMotion) { draw(3); return; }
        host.addEventListener("pointermove", function (e) {
            var r = host.getBoundingClientRect();
            pointer.aimX = (e.clientX - r.left) / r.width; pointer.aimY = (e.clientY - r.top) / r.height; pointer.active = true;
        }, { passive: true });
        host.addEventListener("pointerleave", function () { pointer.active = false; });
        var wander = 0;
        loop(host, finePointer ? 40 : 24, function (dt, t) {
            if (!pointer.active) { wander += dt * 0.35; pointer.aimX = 0.5 + Math.sin(wander) * 0.35; pointer.aimY = 0.5 + Math.sin(wander * 1.7) * 0.3; }
            // Inertia: the attractor accelerates toward the pointer and is damped, so it trails and overshoots a little.
            pointer.vx += ((pointer.aimX - pointer.x) * 22 - pointer.vx * 5) * dt; pointer.x += pointer.vx * dt;
            pointer.vy += ((pointer.aimY - pointer.y) * 22 - pointer.vy * 5) * dt; pointer.y += pointer.vy * dt;
            draw(t);
        });
    }

    /* ---------------------------------------------------------------- rooms rail with momentum */
    function rail(root) {
        var track = root.querySelector("[data-t5-track]");
        if (!track) return;
        var section = root.closest("section") || document;
        var prev = section.querySelector("[data-t5-prev]"), next = section.querySelector("[data-t5-next]");
        var x = 0, v = 0, min = 0, raf = 0, last = 0, mode = "idle", target = 0;
        var drag = null, suppress = false;

        function cards() { return Array.prototype.slice.call(track.children); }
        function bounds() {
            // Line the first card up with the section heading, and leave the same room after the last one.
            var head = section.querySelector(".t5-head");
            if (head) {
                var pad = Math.max(16, Math.round(head.getBoundingClientRect().left - root.getBoundingClientRect().left));
                track.style.paddingLeft = track.style.paddingRight = pad + "px";
            }
            min = Math.min(0, root.clientWidth - track.scrollWidth);
            if (x < min) x = min;
            if (x > 0) x = 0;
            apply();
        }
        function snaps() {
            var list = cards(), first = list.length ? list[0].offsetLeft : 0;
            return list.map(function (c) { return Math.max(min, -(c.offsetLeft - first)); });
        }
        function nearest(pos, bias) {
            var s = snaps(), best = 0, bestD = Infinity;
            s.forEach(function (p) { var d = Math.abs(p - pos); if (d < bestD) { bestD = d; best = p; } });
            if (bias) { var idx = s.indexOf(best), ni = Math.max(0, Math.min(s.length - 1, idx + bias)); return s[ni]; }
            return best;
        }
        function apply() {
            track.style.transform = "translate3d(" + x.toFixed(2) + "px,0,0)";
            if (prev) prev.disabled = x >= -1;
            if (next) next.disabled = x <= min + 1;
        }
        function settle(to) {
            target = Math.max(min, Math.min(0, to));
            if (reduceMotion) { x = target; v = 0; apply(); return; }
            mode = "snap"; kick();
        }
        function kick() { if (!raf) { last = 0; raf = requestAnimationFrame(tick); } }
        function tick(t) {
            raf = 0;
            var dt = last ? Math.min((t - last) / 1000, 0.05) : 1 / 60;
            last = t;
            if (mode === "coast") {
                x += v * dt;
                v *= Math.pow(0.0035, dt);                // friction: about a third of the speed is lost every 0.2s
                if (x > 0 || x < min) {                    // past an end: the band pulls it back hard
                    var edge = x > 0 ? 0 : min;
                    v += (edge - x) * 120 * dt; v *= Math.pow(0.02, dt);
                }
                if (Math.abs(v) < 160) settle(nearest(x + v * 0.18));
            } else if (mode === "snap") {
                v += ((target - x) * 140 - v * 22) * dt;   // a damped spring onto the card edge
                x += v * dt;
                if (Math.abs(target - x) < 0.4 && Math.abs(v) < 4) { x = target; v = 0; mode = "idle"; }
            }
            apply();
            if (mode !== "idle") raf = requestAnimationFrame(tick);
        }

        root.addEventListener("pointerdown", function (e) {
            if (e.button !== 0) return;
            if (raf) { cancelAnimationFrame(raf); raf = 0; }
            mode = "idle";
            drag = { id: e.pointerId, sx: e.clientX, sy: e.clientY, x0: x, moved: false, samples: [{ x: e.clientX, t: e.timeStamp }], locked: false };
            suppress = false;
        });
        root.addEventListener("pointermove", function (e) {
            if (!drag || e.pointerId !== drag.id) return;
            var dx = e.clientX - drag.sx, dy = e.clientY - drag.sy;
            if (!drag.locked) {
                if (Math.abs(dx) < 6 && Math.abs(dy) < 6) return;
                if (Math.abs(dy) > Math.abs(dx)) { drag = null; return; }   // vertical: let the page scroll
                drag.locked = true; drag.moved = true;
                root.classList.add("is-dragging");
                try { root.setPointerCapture(e.pointerId); } catch (err) { /* older browsers */ }
            }
            var nx = drag.x0 + dx;
            if (nx > 0) nx = nx * 0.35; else if (nx < min) nx = min + (nx - min) * 0.35;   // rubber band
            x = nx; apply();
            drag.samples.push({ x: e.clientX, t: e.timeStamp });
            if (drag.samples.length > 6) drag.samples.shift();
        });
        function release(e) {
            if (!drag || (e && e.pointerId !== drag.id)) return;
            var d = drag; drag = null;
            root.classList.remove("is-dragging");
            if (!d.moved) return;
            suppress = true;
            var a = d.samples[0], b = d.samples[d.samples.length - 1], span = Math.max(1, b.t - a.t);
            v = (b.x - a.x) / span * 1000;
            if (Math.abs(v) > 6000) v = 6000 * Math.sign(v);
            if (reduceMotion) { settle(nearest(x, v > 300 ? -1 : v < -300 ? 1 : 0)); return; }
            mode = "coast"; kick();
        }
        root.addEventListener("pointerup", release);
        root.addEventListener("pointercancel", release);
        // A drag must not open the room page or the photo viewer when the pointer lifts over a card.
        root.addEventListener("click", function (e) { if (suppress) { e.preventDefault(); e.stopPropagation(); suppress = false; } }, true);
        root.addEventListener("dragstart", function (e) { e.preventDefault(); });

        // Trackpads: horizontal swipes move the rail directly, then it settles.
        var wheelTimer = 0;
        root.addEventListener("wheel", function (e) {
            if (Math.abs(e.deltaX) <= Math.abs(e.deltaY)) return;
            e.preventDefault();
            if (raf) { cancelAnimationFrame(raf); raf = 0; mode = "idle"; }
            x = Math.max(min - 40, Math.min(40, x - e.deltaX)); apply();
            clearTimeout(wheelTimer);
            wheelTimer = setTimeout(function () { settle(nearest(x)); }, 140);
        }, { passive: false });

        if (prev) prev.addEventListener("click", function () { settle(nearest(x, -1)); });
        if (next) next.addEventListener("click", function () { settle(nearest(x, 1)); });
        // Keyboard users: tabbing to a card brings it into view.
        track.addEventListener("focusin", function (e) {
            var card = e.target.closest(".t5-room");
            if (!card) return;
            var left = card.offsetLeft + x, right = left + card.offsetWidth;
            if (left < 0 || right > root.clientWidth) settle(-(card.offsetLeft - cards()[0].offsetLeft));
            root.scrollLeft = 0;
        });
        window.addEventListener("resize", function () { bounds(); settle(nearest(x)); });
        bounds();
    }

    function init() {
        Array.prototype.forEach.call(document.querySelectorAll("[data-t5-world]"), world);
        Array.prototype.forEach.call(document.querySelectorAll("[data-t5-blobs]"), blobs);
        Array.prototype.forEach.call(document.querySelectorAll("[data-t5-rail]"), rail);
        buttons();
        tvPower();
    }
    if (document.readyState === "loading") document.addEventListener("DOMContentLoaded", init); else init();
})();
