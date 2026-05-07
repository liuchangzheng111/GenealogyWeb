using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
{
    /// <summary>
    /// 用户与族谱的多对多：表示「受邀协作」或创建时的 Owner 成员关系。
    /// </summary>
    /// <remarks>
    /// 建议 <see cref="Role"/> 取值：<c>Owner</c>、<c>Editor</c>、<c>Viewer</c>（字符串约定，后续可做枚举或权限表）。
    /// </remarks>
    public class GenealogyUser
    {
        [Key]
        public int Id { get; set; }

        public Guid GenealogyId { get; set; }
        public Guid UserId { get; set; }

        /// <summary>在族谱内的角色（约定字符串）。</summary>
        public string Role { get; set; } = "Editor";

        /// <summary>邀请人；创建者自建族谱时可为 null。</summary>
        public Guid? InvitedByUserId { get; set; }

        public DateTime InvitedAt { get; set; } = DateTime.UtcNow;
    }
}
