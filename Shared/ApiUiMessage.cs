using System.Net;
using System.Text.Json;

namespace GenealogyWeb.Shared;

/// <summary>
/// 将 API 失败响应整理为适合直接展示给用户的短句（支持纯文本、JSON message、ProblemDetails、ModelState errors）。
/// </summary>
public static class ApiUiMessage
{
    /// <summary>与 ASP.NET Core 默认 camelCase JSON 对齐的反序列化选项。</summary>
    public static readonly JsonSerializerOptions JsonReadOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };
    public static async Task<string> FormatAsync(HttpResponseMessage response, CancellationToken cancellationToken = default)
    {
        var text = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(text))
        {
            return DefaultForStatus(response.StatusCode);
        }

        var trimmed = text.Trim();
        if (trimmed.StartsWith("{", StringComparison.Ordinal))
        {
            try
            {
                using var doc = JsonDocument.Parse(trimmed);
                var root = doc.RootElement;
                if (root.TryGetProperty("message", out var msgEl) && msgEl.ValueKind == JsonValueKind.String)
                {
                    var s = msgEl.GetString();
                    if (!string.IsNullOrWhiteSpace(s))
                    {
                        return s!;
                    }
                }

                if (root.TryGetProperty("title", out var titleEl))
                {
                    var title = titleEl.ValueKind == JsonValueKind.String ? titleEl.GetString() ?? "" : "";
                    if (root.TryGetProperty("detail", out var detailEl) && detailEl.ValueKind == JsonValueKind.String)
                    {
                        var detail = detailEl.GetString();
                        if (!string.IsNullOrWhiteSpace(detail))
                        {
                            return string.IsNullOrWhiteSpace(title) ? detail! : $"{title}：{detail}";
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(title))
                    {
                        return title!;
                    }
                }

                if (root.TryGetProperty("errors", out var errors) && errors.ValueKind == JsonValueKind.Object)
                {
                    var parts = new List<string>();
                    foreach (var prop in errors.EnumerateObject())
                    {
                        foreach (var err in prop.Value.EnumerateArray())
                        {
                            if (err.ValueKind == JsonValueKind.String)
                            {
                                var e = err.GetString();
                                if (!string.IsNullOrWhiteSpace(e))
                                {
                                    parts.Add($"{prop.Name}：{e}");
                                }
                            }
                        }
                    }

                    if (parts.Count > 0)
                    {
                        return string.Join("；", parts);
                    }
                }
            }
            catch (JsonException)
            {
                // 非预期 JSON，退回原文
            }
        }

        return trimmed;
    }

    private static string DefaultForStatus(HttpStatusCode code) => code switch
    {
        HttpStatusCode.Unauthorized => "请先登录或会话已过期，请重新登录。",
        HttpStatusCode.Forbidden => "没有权限执行此操作。",
        HttpStatusCode.NotFound => "请求的资源不存在。",
        HttpStatusCode.Conflict => "与现有数据冲突，请刷新后重试。",
        _ => $"请求失败（{(int)code}）。"
    };
}
