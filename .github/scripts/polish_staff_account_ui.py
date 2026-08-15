from pathlib import Path

operational = Path('src/DeLong.Web/wwwroot/css/operational-polish.css')
text = operational.read_text()
marker = '/* Shared action / account polish */'
if marker not in text:
    text += r'''

/* Shared action / account polish */
.btn {
    white-space:nowrap;
    flex-shrink:0;
}
.table-actions,
.modal-actions {
    flex-wrap:wrap;
}
.row-action {
    min-width:76px;
    white-space:nowrap;
}

/* Shared entity toolbar primitives. Page-specific entity CSS may override these. */
.entity-toolbar {
    min-height:76px;
    padding:16px 20px;
    display:flex;
    align-items:center;
    justify-content:space-between;
    gap:20px;
    border-bottom:1px solid var(--line);
    background:#fff;
}
.entity-toolbar-main {
    min-width:0;
    flex:1 1 auto;
    display:flex;
    align-items:center;
    gap:12px;
    flex-wrap:nowrap;
}
.entity-toolbar-main .search-input {
    width:auto;
    min-width:280px;
    max-width:560px;
    flex:1 1 420px;
    margin:0;
}
.entity-count {
    flex:0 0 auto;
    min-width:92px;
    padding:7px 11px;
    border-radius:999px;
    background:var(--surface-soft);
    color:var(--muted);
    font-size:11px;
    font-weight:700;
    text-align:center;
    white-space:nowrap;
}
.entity-cell {
    display:flex;
    align-items:center;
    gap:10px;
    min-width:0;
}
.entity-avatar {
    width:34px;
    height:34px;
    flex:0 0 34px;
    border-radius:10px;
    display:grid;
    place-items:center;
    background:var(--brand-100);
    color:var(--brand-800);
    font-size:11px;
    font-weight:850;
    text-transform:uppercase;
}
.entity-copy {
    min-width:0;
    display:grid;
    gap:2px;
}
.entity-copy strong {
    overflow:hidden;
    text-overflow:ellipsis;
    white-space:nowrap;
}
.entity-copy span {
    color:var(--muted);
    font-size:10px;
    white-space:nowrap;
}

.topbar-password-link {
    min-height:38px;
    padding-left:11px;
    padding-right:11px;
}
.account-security-card {
    width:min(500px,100%);
    padding:30px;
    gap:18px;
}
.account-security-heading {
    display:grid;
    gap:8px;
}
.account-security-heading .eyebrow {
    width:max-content;
}
.account-security-heading h1,
.account-security-heading p {
    margin:0;
}
.account-security-heading p {
    line-height:1.65;
}
.account-security-card .field {
    gap:8px;
}
.account-security-hint {
    padding:11px 13px;
    border:1px solid #d8e8e3;
    border-radius:11px;
    background:var(--brand-50);
    color:var(--text-soft);
    font-size:11px;
    line-height:1.55;
}
.auth-actions {
    margin-top:2px;
    display:flex;
    align-items:center;
    justify-content:flex-end;
    gap:10px;
    flex-wrap:wrap;
}

@media (max-width:680px) {
    .entity-toolbar {
        min-height:0;
        padding:12px;
        align-items:stretch;
        flex-direction:column;
        gap:10px;
    }
    .entity-toolbar-main {
        width:100%;
        display:grid;
        grid-template-columns:1fr;
        gap:9px;
    }
    .entity-toolbar-main .search-input {
        width:100%;
        max-width:none;
        min-width:0;
        flex:none;
    }
    .entity-count { align-self:flex-start; }
    .topbar-password-link {
        width:40px;
        min-height:40px;
        padding:0;
    }
    .topbar-password-link span { display:none; }
    .account-security-card {
        padding:22px 18px;
    }
    .auth-actions .btn {
        flex:1 1 auto;
    }
}
'''
    operational.write_text(text)

staff = Path('src/DeLong.Web/wwwroot/css/staff.css')
text = staff.read_text()
marker = '/* Staff UAT spacing polish */'
if marker not in text:
    text += r'''

/* Staff UAT spacing polish */
.staff-page-head { margin-bottom:24px; }
.staff-page-head p { margin-top:9px; }
.staff-stat-grid { gap:16px; margin-bottom:20px; }
.staff-stat { min-height:118px; padding:19px 20px; gap:6px; }
.staff-stat small { line-height:1.45; }
.staff-role-guide { margin-bottom:20px; padding:22px; }
.staff-section-head { margin-bottom:18px; }
.staff-role-guide-grid { gap:12px; }
.role-guide-card { min-height:106px; padding:14px; gap:11px; }
.role-guide-card p { font-size:10px; line-height:1.62; }
.staff-toolbar { gap:18px; }
.staff-toolbar-main { gap:11px; }
.staff-table th:nth-child(1) { width:27%; }
.staff-table th:nth-child(2) { width:13%; }
.staff-table th:nth-child(3) { width:20%; }
.staff-table th:nth-child(4) { width:17%; }
.staff-table th:nth-child(5) { width:13%; }
.staff-table th:nth-child(6) { width:92px; }
.staff-table td { padding-top:15px; padding-bottom:15px; }
.staff-table th:last-child,
.staff-table td:last-child { white-space:nowrap; text-align:right; }
.staff-table .row-action { min-width:82px; }
.staff-editor { width:min(960px,calc(100vw - 40px)); }
.staff-form-section { padding:22px 24px; }
.staff-form-title { margin-bottom:16px; gap:12px; }
.staff-form-title>span { width:30px; height:30px; flex-basis:30px; }
.staff-form-title>div { gap:3px; }
.staff-form-title strong { font-size:12px; }
.staff-form-title small { font-size:10px; line-height:1.5; }
.role-picker { gap:10px; }
.role-picker-card { min-height:124px; padding:14px; gap:10px; }
.property-picker { gap:10px; }
.property-choice { padding:13px 14px; }
.staff-security-actions { gap:18px; }
.account-active-toggle { min-height:72px; padding:13px 14px; }
.staff-notice.info { margin:16px 24px 6px; }
.staff-reset-modal { width:min(540px,calc(100vw - 32px)); }
.staff-reset-modal>.staff-notice.warning { margin:18px 22px 16px; }
.staff-reset-modal>.field { margin:0 22px; }
.staff-reset-modal>.btn { margin:14px 22px 2px; width:max-content; }
.staff-reset-modal .modal-actions { margin-top:20px; }

@media (max-width:820px) {
    .staff-stat-grid { gap:12px; }
    .staff-role-guide { padding:18px; }
    .staff-editor { width:min(720px,calc(100vw - 24px)); }
}

@media (max-width:560px) {
    .staff-page-head { margin-bottom:20px; }
    .staff-stat-grid { gap:10px; margin-bottom:16px; }
    .staff-stat { padding:15px; }
    .staff-role-guide { margin-bottom:16px; padding:15px; }
    .staff-section-head { margin-bottom:14px; }
    .staff-form-section { padding:18px 16px; }
    .staff-notice.info { margin:14px 16px 4px; }
    .staff-reset-modal>.staff-notice.warning { margin:16px 16px 14px; }
    .staff-reset-modal>.field { margin-left:16px; margin-right:16px; }
    .staff-reset-modal>.btn { margin-left:16px; margin-right:16px; }
}
'''
    staff.write_text(text)

shell = Path('src/DeLong.Web/wwwroot/js/core/shell.js')
text = shell.read_text()
marker = 'topbar-password-link'
if marker not in text:
    needle = "    window.matchMedia('(min-width: 981px)').addEventListener('change', event => {\n"
    addition = r'''    const userChip = document.querySelector('.admin-topbar .user-chip');
    if (userChip && !document.querySelector('.topbar-password-link')) {
        const passwordLink = document.createElement('a');
        const returnUrl = `${window.location.pathname}${window.location.search}`;
        passwordLink.className = 'btn btn-ghost btn-sm topbar-password-link';
        passwordLink.href = `/Account/ChangePassword?returnUrl=${encodeURIComponent(returnUrl)}`;
        passwordLink.title = 'Đổi mật khẩu';
        passwordLink.setAttribute('aria-label', 'Đổi mật khẩu');
        passwordLink.innerHTML = '<svg aria-hidden="true"><use href="#i-settings"></use></svg><span>Đổi mật khẩu</span>';
        userChip.insertAdjacentElement('afterend', passwordLink);
    }

'''
    if needle not in text:
        raise SystemExit('shell.js insertion point not found')
    text = text.replace(needle, addition + needle, 1)
    shell.write_text(text)

change_password = Path('src/DeLong.Web/Pages/Account/ChangePassword.cshtml')
change_password.write_text(r'''@page
@model DeLong.Web.Pages.Account.ChangePasswordModel
@{
    ViewData["Title"] = Model.IsRequired ? "Đổi mật khẩu tạm" : "Đổi mật khẩu";
    var backUrl = Model.ReturnUrl ?? Url.Page("/Admin/Index") ?? "/Admin";
}
<section class="auth-wrap account-security-wrap">
    <form method="post" class="auth-card account-security-card">
        <div class="account-security-heading">
            <span class="eyebrow">Bảo mật tài khoản</span>
            <h1>@(Model.IsRequired ? "Tạo mật khẩu riêng của bạn" : "Đổi mật khẩu")</h1>
            @if (Model.IsRequired)
            {
                <p class="muted">Bạn đang đăng nhập bằng mật khẩu tạm do quản trị viên cấp. Hãy đổi mật khẩu trước khi tiếp tục sử dụng hệ thống.</p>
            }
            else
            {
                <p class="muted">Đổi mật khẩu của tài khoản đang đăng nhập. Sau khi lưu, phiên hiện tại sẽ được làm mới và bạn có thể tiếp tục công việc.</p>
            }
        </div>

        <input type="hidden" name="returnUrl" value="@Model.ReturnUrl" />
        <div asp-validation-summary="ModelOnly" class="validation"></div>

        <div class="field">
            <label asp-for="Input.CurrentPassword"></label>
            <input asp-for="Input.CurrentPassword" autocomplete="current-password" />
            <span asp-validation-for="Input.CurrentPassword" class="validation"></span>
        </div>
        <div class="field">
            <label asp-for="Input.NewPassword"></label>
            <input asp-for="Input.NewPassword" autocomplete="new-password" />
            <span asp-validation-for="Input.NewPassword" class="validation"></span>
        </div>
        <div class="field">
            <label asp-for="Input.ConfirmPassword"></label>
            <input asp-for="Input.ConfirmPassword" autocomplete="new-password" />
            <span asp-validation-for="Input.ConfirmPassword" class="validation"></span>
        </div>

        <div class="account-security-hint">Mật khẩu mới cần ít nhất 8 ký tự. Không dùng lại mật khẩu tạm hoặc mật khẩu đang chia sẻ ở dịch vụ khác.</div>

        <div class="auth-actions">
            @if (!Model.IsRequired)
            {
                <a class="btn btn-light" href="@backUrl">Quay lại</a>
            }
            <button class="btn btn-primary" type="submit">Lưu mật khẩu mới</button>
        </div>
    </form>
</section>
''')
