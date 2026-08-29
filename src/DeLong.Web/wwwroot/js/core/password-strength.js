(function () {
    const minimumLength = 8;
    const minimumGroups = 3;
    const requirement = 'Ít nhất 8 ký tự, kết hợp 3 trong 4 nhóm: chữ thường, chữ hoa, số, ký tự đặc biệt.';

    function evaluate(password) {
        const value = String(password || '');
        const groups = [
            /\p{Ll}/u.test(value),
            /\p{Lu}/u.test(value),
            /\p{Nd}/u.test(value),
            /[^\p{L}\p{N}\s]/u.test(value)
        ].filter(Boolean).length;
        const hasMinimumLength = value.length >= minimumLength;
        const valid = hasMinimumLength && groups >= minimumGroups;
        let score = 0;
        if (value) score = !hasMinimumLength || groups < 2 ? 1 : 2;
        if (valid) score = 3;
        if (value.length >= 12 && groups === 4) score = 4;

        const labels = ['Chưa nhập', 'Yếu', 'Trung bình', 'Mạnh', 'Rất mạnh'];
        let message = requirement;
        if (value && !hasMinimumLength) message = `Cần thêm ${minimumLength - value.length} ký tự.`;
        else if (hasMinimumLength && groups < minimumGroups) message = `Cần thêm ${minimumGroups - groups} nhóm ký tự khác.`;
        else if (valid && score < 4) message = 'Đã đạt yêu cầu. Dùng từ 12 ký tự để bảo mật tốt hơn.';
        else if (score === 4) message = 'Mật khẩu có độ bảo mật tốt.';

        return { valid, score, label: labels[score], message, requirement, groups, minimumLength };
    }

    function mount(input) {
        if (!(input instanceof HTMLInputElement)) return null;
        const existing = input.nextElementSibling;
        const meter = existing?.matches('[data-password-strength]')
            ? existing
            : document.createElement('div');
        if (!meter.dataset.passwordStrength) {
            meter.dataset.passwordStrength = 'true';
            meter.className = 'password-strength-meter';
            meter.setAttribute('aria-live', 'polite');
            meter.innerHTML = '<div class="password-strength-bars" aria-hidden="true"><i></i><i></i><i></i><i></i></div><small><strong data-password-strength-label></strong><span data-password-strength-message></span></small>';
            input.insertAdjacentElement('afterend', meter);
        }

        const update = () => {
            const result = evaluate(input.value);
            meter.dataset.level = String(result.score);
            meter.querySelector('[data-password-strength-label]').textContent = result.label;
            meter.querySelector('[data-password-strength-message]').textContent = result.message;
            input.setCustomValidity(result.valid || !input.value ? '' : result.requirement);
            return result;
        };
        input.addEventListener('input', update);
        update();
        return update;
    }

    window.DeLongPassword = Object.freeze({ evaluate, mount, minimumLength, minimumGroups, requirement });
})();
