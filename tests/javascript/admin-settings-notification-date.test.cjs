const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

function loadSettings(notificationSettings = {}) {
    let options;
    vm.runInNewContext(readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-settings.js', 'utf8'), {
        document: {
            getElementById: id => id === 'settings-page-data'
                ? { textContent: JSON.stringify({ notificationSettings }) }
                : {}
        },
        Vue: { createApp: definition => { options = definition; return { mount() {} }; } },
        Intl
    });
    return { ...options.data(), ...options.methods };
}

for (const field of ['lastEmailSentAtUtc', 'lastTelegramSentAtUtc']) {
    test(`settings renders ${field} with a Vietnamese date and time`, () => {
        const timestamp = new Date(2026, 10, 2, 14, 5).toISOString();
        const settings = loadSettings({ [field]: timestamp });
        const label = settings.notificationDate(settings.notification[field]);

        assert.match(label, /02\/11\/2026/);
        assert.match(label, /14:05/);
    });
}

test('settings leaves missing notification timestamps empty', () => {
    const settings = loadSettings();
    for (const value of [null, undefined, '']) {
        assert.equal(settings.notificationDate(value), '');
    }
});
