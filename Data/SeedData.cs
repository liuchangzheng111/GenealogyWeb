using GenealogyWeb.Models;
using GenealogyWeb.Services;

namespace GenealogyWeb.Data
{
    /// <summary>
    /// 开发/演示用种子数据：保证存在可登录的演示账号，以及「张氏族谱」示例（含少量成员与亲子关系）。
    /// 生产环境可改为由运维脚本灌数，或加环境判断跳过。
    /// </summary>
    public static class SeedData
    {
        /// <summary>演示账号邮箱（首次空库时自动创建）。</summary>
        public const string DemoUserEmail = "demo@genealogy.local";

        /// <summary>演示账号密码（仅本地开发使用，勿用于生产）。</summary>
        public const string DemoUserPassword = "Demo123!";

        /// <summary>
        /// 幂等入口：可重复调用；已有库会修补旧数据（如 CreatedByUserId 为空、缺 Owner 的 GenealogyUsers）。
        /// </summary>
        public static void EnsureSeeded(ApplicationDbContext db)
        {
            var demoUser = EnsureDemoUser(db);

            if (!db.Genealogies.Any())
            {
                SeedSampleGenealogy(db, demoUser.Id);
            }
            else
            {
                NormalizeSampleData(db);
            }

            EnsureGenealogyOwnerLinks(db, demoUser.Id);
        }

        /// <summary>若不存在则创建演示用户。</summary>
        private static User EnsureDemoUser(ApplicationDbContext db)
        {
            var existing = db.Users.FirstOrDefault(u => u.Email == DemoUserEmail);
            if (existing != null)
            {
                return existing;
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = "demo",
                Email = DemoUserEmail,
                PasswordHash = PasswordHelper.HashPassword(DemoUserPassword),
                CreatedAt = DateTime.UtcNow
            };
            db.Users.Add(user);
            db.SaveChanges();
            return user;
        }

        /// <summary>插入完整示例族谱（含 ParentChild 边）。</summary>
        private static void SeedSampleGenealogy(ApplicationDbContext db, Guid ownerUserId)
        {
            var genealogy = new Genealogy
            {
                Id = Guid.NewGuid(),
                Title = "张氏族谱",
                Surname = "张",
                CompiledAt = DateTime.UtcNow,
                CreatedByUserId = ownerUserId,
                CreatedAt = DateTime.UtcNow
            };

            var grandfather = new Person
            {
                Id = Guid.NewGuid(),
                GenealogyId = genealogy.Id,
                GivenName = "张国强",
                Gender = "男",
                BirthYear = 1940,
                Bio = "家族上一代长辈",
                CreatedAt = DateTime.UtcNow
            };

            var grandmother = new Person
            {
                Id = Guid.NewGuid(),
                GenealogyId = genealogy.Id,
                GivenName = "李秀兰",
                Gender = "女",
                BirthYear = 1942,
                Bio = "家族长辈",
                CreatedAt = DateTime.UtcNow
            };

            var father = new Person
            {
                Id = Guid.NewGuid(),
                GenealogyId = genealogy.Id,
                GivenName = "张建国",
                Gender = "男",
                BirthYear = 1968,
                Bio = "家庭成员",
                CreatedAt = DateTime.UtcNow
            };

            var aunt = new Person
            {
                Id = Guid.NewGuid(),
                GenealogyId = genealogy.Id,
                GivenName = "张丽",
                Gender = "女",
                BirthYear = 1972,
                Bio = "家庭成员",
                CreatedAt = DateTime.UtcNow
            };

            var son = new Person
            {
                Id = Guid.NewGuid(),
                GenealogyId = genealogy.Id,
                GivenName = "张伟",
                Gender = "男",
                BirthYear = 1995,
                Bio = "年轻一代",
                CreatedAt = DateTime.UtcNow
            };

            db.Genealogies.Add(genealogy);
            // 创建者必须出现在 GenealogyUsers，与 IGenealogyAccessService 的授权逻辑一致。
            db.GenealogyUsers.Add(new GenealogyUser
            {
                GenealogyId = genealogy.Id,
                UserId = ownerUserId,
                Role = "Owner",
                InvitedByUserId = null,
                InvitedAt = DateTime.UtcNow
            });

            db.Persons.AddRange(grandfather, grandmother, father, aunt, son);

            db.ParentChildren.AddRange(
                new ParentChild
                {
                    GenealogyId = genealogy.Id,
                    ParentId = grandfather.Id,
                    ChildId = father.Id,
                    RelationshipType = "father"
                },
                new ParentChild
                {
                    GenealogyId = genealogy.Id,
                    ParentId = grandmother.Id,
                    ChildId = father.Id,
                    RelationshipType = "mother"
                },
                new ParentChild
                {
                    GenealogyId = genealogy.Id,
                    ParentId = grandfather.Id,
                    ChildId = aunt.Id,
                    RelationshipType = "father"
                },
                new ParentChild
                {
                    GenealogyId = genealogy.Id,
                    ParentId = grandmother.Id,
                    ChildId = aunt.Id,
                    RelationshipType = "mother"
                },
                new ParentChild
                {
                    GenealogyId = genealogy.Id,
                    ParentId = father.Id,
                    ChildId = son.Id,
                    RelationshipType = "father"
                }
            );

            db.SaveChanges();
        }

        /// <summary>历史示例人名修正（兼容早期种子数据）。</summary>
        private static void NormalizeSampleData(ApplicationDbContext db)
        {
            var sampleGenealogy = db.Genealogies.FirstOrDefault(g => g.Title == "张氏族谱" && g.Surname == "张");
            if (sampleGenealogy == null)
            {
                return;
            }

            var renameMap = new Dictionary<string, string>
            {
                ["张老爷子"] = "张国强",
                ["李奶奶"] = "李秀兰"
            };

            var persons = db.Persons.Where(p => p.GenealogyId == sampleGenealogy.Id).ToList();
            var changed = false;
            foreach (var person in persons)
            {
                if (renameMap.TryGetValue(person.GivenName, out var newName))
                {
                    person.GivenName = newName;
                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }

        /// <summary>
        /// 迁移旧数据：补全 Genealogy.CreatedByUserId，并保证创建者在 GenealogyUsers 中有 Owner 行，
        /// 否则族谱访问服务可能拒绝访问。
        /// </summary>
        private static void EnsureGenealogyOwnerLinks(ApplicationDbContext db, Guid demoUserId)
        {
            var changed = false;
            foreach (var g in db.Genealogies.ToList())
            {
                if (g.CreatedByUserId == Guid.Empty)
                {
                    g.CreatedByUserId = demoUserId;
                    changed = true;
                }

                var hasCreatorRow = db.GenealogyUsers.Any(gu => gu.GenealogyId == g.Id && gu.UserId == g.CreatedByUserId);
                if (!hasCreatorRow)
                {
                    db.GenealogyUsers.Add(new GenealogyUser
                    {
                        GenealogyId = g.Id,
                        UserId = g.CreatedByUserId,
                        Role = "Owner",
                        InvitedByUserId = null,
                        InvitedAt = DateTime.UtcNow
                    });
                    changed = true;
                }
            }

            if (changed)
            {
                db.SaveChanges();
            }
        }
    }
}
