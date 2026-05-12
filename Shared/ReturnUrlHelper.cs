namespace GenealogyWeb.Shared;

/// <summary>校验登录后回跳地址，防止开放重定向。</summary>
public static class ReturnUrlHelper
{
    public static bool IsSafeLocalPath(string? url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return false;
        }

        var u = url.Trim();
        if (!u.StartsWith('/'))
        {
            return false;
        }

        if (u.StartsWith("//", StringComparison.Ordinal))
        {
            return false;
        }

        if (u.Contains("://", StringComparison.Ordinal) || u.Contains('\\'))
        {
            return false;
        }

        // 避免 javascript: 等伪协议经编码混入（保守：仅允许常见路径字符）
        var i = u.IndexOf(':', StringComparison.Ordinal);
        if (i >= 0 && i < 10)
        {
            return false;
        }

        return true;
    }
}
