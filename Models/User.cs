using System.ComponentModel.DataAnnotations;

namespace GenealogyApp.Models
{
    /// <summary>应用注册用户：登录标识为 <see cref="Email"/>（唯一性在业务层校验）。</summary>
    public class User
    {
        [Key]
        public Guid Id { get; set; }

        /// <summary>显示名，可与邮箱不同。</summary>
        [Required]
        public string UserName { get; set; } = null!;

        /// <summary>登录邮箱。</summary>
        [Required]
        public string Email { get; set; } = null!;

        /// <summary>由 PasswordHelper 生成的哈希，禁止存明文。</summary>
        [Required]
        public string PasswordHash { get; set; } = null!;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
