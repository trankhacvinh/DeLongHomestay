(() => {
    const root = document.querySelector('[data-ai-settings]'); if (!root || !window.DeLongApi) return;
    const id = root.dataset.propertyId; const $ = selector => root.querySelector(selector);
    const message = $('[data-ai-settings-message]');
    function show(text, error = false) { message.hidden = false; message.textContent = text; message.classList.toggle('error', error); }
    async function load() {
        try {
            const [profile, usage] = await Promise.all([DeLongApi.get(`/api/admin/properties/${id}/ai/profile`), DeLongApi.get(`/api/admin/properties/${id}/ai/usage`)]);
            $('[data-ai-enabled]').checked = profile.isEnabled; $('[data-ai-provider]').value = profile.provider; $('[data-ai-model]').value = profile.model;
            $('[data-ai-max-tokens]').value = profile.maxOutputTokens; $('[data-ai-monthly-limit]').value = profile.monthlyTokenLimit;
            $('[data-ai-budget]').value = profile.monthlyBudgetUsd; $('[data-ai-budget-warning]').value = profile.budgetWarningPercent;
            $('[data-ai-input-cost]').value = profile.inputCostPerMillionTokensUsd; $('[data-ai-output-cost]').value = profile.outputCostPerMillionTokensUsd;
            $('[data-ai-key-status]').textContent = profile.apiKeyConfigured ? 'API key đã được lưu mã hóa. Để trống nếu không thay đổi.' : 'Chưa có API key.';
            $('[data-ai-calls]').textContent = usage.calls.toLocaleString('vi-VN'); $('[data-ai-input]').textContent = usage.inputTokens.toLocaleString('vi-VN'); $('[data-ai-output]').textContent = usage.outputTokens.toLocaleString('vi-VN'); $('[data-ai-total]').textContent = usage.totalTokens.toLocaleString('vi-VN');
            $('[data-ai-cost]').textContent = `$${Number(usage.estimatedCostUsd).toFixed(4)}`;
            $('[data-ai-remaining]').textContent = usage.monthlyBudgetUsd > 0 ? `$${Number(usage.remainingBudgetUsd).toFixed(2)}` : 'Không giới hạn';
            const budgetMessage = $('[data-ai-budget-message]');
            budgetMessage.textContent = usage.monthlyBudgetUsd > 0
                ? `Đã dùng khoảng ${usage.usedBudgetPercent}% ngân sách $${Number(usage.monthlyBudgetUsd).toFixed(2)}. Chi phí là ước tính theo đơn giá token bạn cấu hình.`
                : 'Chưa đặt ngân sách tiền. Chi phí chỉ được tính khi đã nhập đơn giá input/output của model.';
            budgetMessage.classList.toggle('error', usage.isBudgetWarning);
        } catch (error) { show(error.message, true); }
    }
    $('[data-ai-provider]').addEventListener('change', event => { const model = $('[data-ai-model]'); if (!model.value || /^(gpt|gemini)/.test(model.value)) model.value = event.target.value === '1' ? 'gemini-2.5-flash' : 'gpt-5-mini'; });
    $('[data-ai-save]').addEventListener('click', async event => {
        event.currentTarget.disabled = true;
        try {
            await DeLongApi.put(`/api/admin/properties/${id}/ai/profile`, { isEnabled: $('[data-ai-enabled]').checked, provider: Number($('[data-ai-provider]').value), model: $('[data-ai-model]').value.trim(), apiKey: $('[data-ai-key]').value.trim() || null, clearApiKey: false, maxOutputTokens: Number($('[data-ai-max-tokens]').value), monthlyTokenLimit: Number($('[data-ai-monthly-limit]').value), monthlyBudgetUsd: Number($('[data-ai-budget]').value), inputCostPerMillionTokensUsd: Number($('[data-ai-input-cost]').value), outputCostPerMillionTokensUsd: Number($('[data-ai-output-cost]').value), budgetWarningPercent: Number($('[data-ai-budget-warning]').value) });
            $('[data-ai-key]').value = ''; show('Đã lưu cấu hình AI. Bạn có thể mở nút AI trên thanh trên cùng để thử.'); await load();
        } catch (error) { show(error.message, true); } finally { event.currentTarget.disabled = false; }
    });
    load();
})();
