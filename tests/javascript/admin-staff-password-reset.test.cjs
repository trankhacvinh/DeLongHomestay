const { test } = require('node:test');
const assert = require('node:assert/strict');
const { readFileSync } = require('node:fs');
const vm = require('node:vm');

function openPasswordReset(api) {
    const account = { id: 'staff-id', canManage: true, properties: [{ id: 'property-id' }] };
    let options;
    vm.runInNewContext(readFileSync('src/DeLong.Web/wwwroot/js/pages/admin-staff.js', 'utf8'), {
        document: {
            getElementById: id => id === 'staff-page-data'
                ? { textContent: JSON.stringify({ accounts: [account] }) }
                : {}
        },
        Vue: {
            createApp(definition) {
                options = definition;
                return { mount() {} };
            }
        },
        DeLongApi: api,
        setTimeout: () => 1,
        clearTimeout() {}
    });
    const state = options.data();
    for (const [name, method] of Object.entries(options.methods)) state[name] = method.bind(state);
    state.openEdit(account);
    state.openResetPassword();
    state.resetModal.password = 'Test-pw-123';
    return state;
}

const unauthorized = () => Object.assign(new Error('HTTP 401'), { status: 401 });

test('reset POST returning 401 clears the temporary password and offers sign-in without resubmitting', async () => {
    let posts = 0;
    const state = openPasswordReset({
        post: async () => {
            posts++;
            throw unauthorized();
        }
    });

    await state.resetPassword();
    assert.equal(state.resetModal.open, true);
    assert.equal(state.resetModal.sessionExpired, true);
    assert.equal(state.resetModal.password, '');
    await state.resetPassword();
    assert.equal(posts, 1);
});

test('successful reset stays successful when the follow-up staff refresh returns 401', async () => {
    const state = openPasswordReset({
        post: async () => null,
        get: async () => { throw unauthorized(); }
    });

    await state.resetPassword();
    assert.equal(state.resetModal.open, false);
    assert.equal(state.resetModal.password, '');
    assert.equal(state.accounts[0].mustChangePassword, true);
    assert.equal(state.editor.account.mustChangePassword, true);
    assert.equal(state.toast.type, 'success');
    assert.match(state.toast.message, /Đã đặt lại mật khẩu/);
});

test('password validation errors keep the modal available for correction', async () => {
    const state = openPasswordReset({
        post: async () => { throw Object.assign(new Error('Mật khẩu không hợp lệ.'), { status: 400 }); }
    });

    await state.resetPassword();
    assert.equal(state.resetModal.open, true);
    assert.equal(state.resetModal.sessionExpired, false);
    assert.equal(state.resetModal.password, 'Test-pw-123');
    assert.equal(state.toast.message, 'Mật khẩu không hợp lệ.');
});
