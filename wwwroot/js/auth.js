/**
 * 登录 / 注册页使用的轻量脚本：通过 fetch + credentials:include 与 Cookie 认证配合。
 * Blazor 内受保护数据请使用注入了 CookieForwardingHandler 的 HttpClient，勿直接复用本文件逻辑。
 */

window.authPost = async function (url, data) {
    const resp = await fetch(url, {
        method: 'POST',
        credentials: 'include',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(data)
    });
    const text = await resp.text();
    let body = text;
    try { body = JSON.parse(text); } catch { }
    return { status: resp.status, body: body };
}

/** 在指定 div 上显示成功/错误样式消息 */
function authMessage(targetId, text, kind) {
    const target = document.getElementById(targetId);
    if (!target) return;
    target.className = kind === 'success' ? 'alert alert-success mt-2' : 'alert alert-danger mt-2';
    target.textContent = text;
    target.style.display = 'block';
}

/** 注册按钮：属性名与 AuthController.RegisterDto 对齐（PascalCase，ASP.NET 默认 JSON 绑定兼容 camelCase） */
window.registerSubmit = async function () {
    const userName = document.getElementById('register-userName')?.value || '';
    const email = document.getElementById('register-email')?.value || '';
    const password = document.getElementById('register-password')?.value || '';
    const result = await window.authPost('/api/auth/register', { UserName: userName, Email: email, Password: password });

    if (result.status >= 200 && result.status < 300) {
        authMessage('register-message', result.body?.message || '注册成功，请前往登录。', 'success');
        document.getElementById('register-userName').value = '';
        document.getElementById('register-email').value = '';
        document.getElementById('register-password').value = '';
        setTimeout(() => { window.location.href = '/login'; }, 900);
        return;
    }

    authMessage('register-message', result.body?.message || '注册失败', 'danger');
}

/** 登录按钮 */
window.loginSubmit = async function () {
    const email = document.getElementById('login-email')?.value || '';
    const password = document.getElementById('login-password')?.value || '';
    const result = await window.authPost('/api/auth/login', { Email: email, Password: password });

    if (result.status >= 200 && result.status < 300) {
        authMessage('login-message', result.body?.message || '登录成功，正在跳转...', 'success');
        setTimeout(() => { window.location.href = '/'; }, 900);
        return;
    }

    authMessage('login-message', result.body?.message || '登录失败', 'danger');
}

/** 若页面存在 id=current-user-info 则拉取 /api/auth/me（可选，部分页面未用） */
window.loadCurrentUser = async function () {
    const target = document.getElementById('current-user-info');
    if (!target) return;

    try {
        const resp = await fetch('/api/auth/me', { credentials: 'include' });
        const data = await resp.json();

        if (data.isAuthenticated) {
            target.className = 'alert alert-success';
            target.innerHTML = `当前登录用户：<strong>${data.userName ?? ''}</strong>，邮箱：<strong>${data.email ?? ''}</strong>`;
        } else {
            target.className = 'alert alert-secondary';
            target.textContent = data.message || '未登录';
        }

        target.style.display = 'block';
    } catch {
        target.className = 'alert alert-danger';
        target.textContent = '无法获取当前用户信息';
        target.style.display = 'block';
    }
}

document.addEventListener('DOMContentLoaded', () => {
    window.loadCurrentUser?.();
});

window.loadCurrentUser?.();
