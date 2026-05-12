namespace GenealogyWeb.Shared;

/// <summary>协作角色在界面上的中文说明（与数据库约定字符串一致）。</summary>
public static class RoleUiText
{
    public static string ToChinese(string? role) => role?.Trim().ToUpperInvariant() switch
    {
        "OWNER" => "族谱管理员",
        "EDITOR" => "可编辑",
        "VIEWER" => "只读",
        _ => string.IsNullOrWhiteSpace(role) ? "—" : role!
    };
}
