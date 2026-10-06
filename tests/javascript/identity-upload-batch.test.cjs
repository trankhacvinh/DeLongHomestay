const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const source = readFileSync('src/DeLong.Web/wwwroot/js/pages/public-booking-core-v2.js', 'utf8');
const block = source.slice(source.indexOf('                const documents ='), source.indexOf('\n            }\n            return result;', source.indexOf('                const documents =')));
const run = new (Object.getPrototypeOf(async function() {}).constructor)('state', 'uploadIdentity', 'result', 'requestKey', block);

test('All four documents upload with at most two simultaneous requests', async () => {
    let active = 0, peak = 0;
    const sides = [];
    await run({ front: {}, back: {}, secondFront: {}, secondBack: {} }, async (_, side) => {
        sides.push(side); active++; peak = Math.max(peak, active);
        await new Promise(resolve => setImmediate(resolve)); active--;
    }, { bookingId: 'b' }, 'key');
    assert.equal(peak, 2);
    assert.deepEqual(sides, ['front', 'back', 'second-front', 'second-back']);
});

test('Failed batch waits for its other request before reporting failure and stops subsequent uploads', async () => {
    let otherFinished = false;
    const sides = [];
    await assert.rejects(run({ front: {}, back: {}, secondFront: {} }, async (_, side) => {
        sides.push(side);
        if (side === 'front') throw new Error('upload failed');
        await new Promise(resolve => setImmediate(resolve)); otherFinished = true;
    }, { bookingId: 'b' }, 'key'), /upload failed/);
    assert.equal(otherFinished, true);
    assert.deepEqual(sides, ['front', 'back']);
});
