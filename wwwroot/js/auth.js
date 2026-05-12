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

/** 将 API 返回体整理为一句可读中文/英文错误 */
function pickAuthErrorMessage(status, body) {
    const fallback = (code) => {
        if (code === 401) return '邮箱或密码不正确，或账户不存在。';
        if (code === 409) return '该邮箱已被注册。';
        if (code === 400) return '请检查输入内容。';
        return '请求失败（' + code + '）。';
    };

    if (body == null || body === '') return fallback(status);

    if (typeof body === 'string') {
        const t = body.trim();
        return t.length ? t : fallback(status);
    }

    if (typeof body === 'object') {
        if (body.message) return String(body.message);
        if (body.title && body.detail) return String(body.title) + '：' + String(body.detail);
        if (body.title) return String(body.title);
        if (body.errors && typeof body.errors === 'object') {
            const parts = [];
            for (const k of Object.keys(body.errors)) {
                const arr = body.errors[k];
                if (Array.isArray(arr)) {
                    arr.forEach((x) => parts.push(k + '：' + x));
                }
            }
            if (parts.length) return parts.join('；');
        }
    }

    return fallback(status);
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

    authMessage('register-message', pickAuthErrorMessage(result.status, result.body), 'danger');
}

/** 登录成功后的站内回跳（由 Login 页 hidden 提供，须为以 / 开头的相对路径） */
function readSafeReturnUrl() {
    const el = document.getElementById('login-returnUrl');
    const v = el?.value?.trim();
    if (v && v.startsWith('/') && !v.startsWith('//') && !v.includes('://')) {
        return v;
    }
    return '/';
}

/** 登录按钮 */
window.loginSubmit = async function () {
    const email = document.getElementById('login-email')?.value || '';
    const password = document.getElementById('login-password')?.value || '';
    const result = await window.authPost('/api/auth/login', { Email: email, Password: password });

    if (result.status >= 200 && result.status < 300) {
        authMessage('login-message', result.body?.message || '登录成功，正在跳转...', 'success');
        const target = readSafeReturnUrl();
        setTimeout(() => { window.location.href = target; }, 900);
        return;
    }

    authMessage('login-message', pickAuthErrorMessage(result.status, result.body), 'danger');
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
