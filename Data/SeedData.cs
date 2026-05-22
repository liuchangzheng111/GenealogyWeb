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
            // 如果没有王氏族谱，则生成一个较大的测试谱（10代、约500人）以便演示与性能测试
            if (!db.Genealogies.Any(g => g.Surname == "王"))
            {
                SeedLargeWangGenealogy(db, demoUser.Id, generations: 10, targetMembers: 500);
            }
        }

        /// <summary>
        /// 生成较大的王氏族谱用于测试与演示（多代树与父母/婚姻关系）。
        /// </summary>
        private static void SeedLargeWangGenealogy(ApplicationDbContext db, Guid ownerUserId, int generations = 10, int targetMembers = 500)
        {
            if (generations < 2) generations = 10;
            if (targetMembers < 10) targetMembers = 500;

            var genealogy = new Genealogy
            {
                Id = Guid.NewGuid(),
                Title = "王氏族谱",
                Surname = "王",
                CompiledAt = DateTime.UtcNow,
                CreatedByUserId = ownerUserId,
                CreatedAt = DateTime.UtcNow
            };

            db.Genealogies.Add(genealogy);
            db.GenealogyUsers.Add(new GenealogyUser
            {
                GenealogyId = genealogy.Id,
                UserId = ownerUserId,
                Role = "Owner",
                InvitedByUserId = null,
                InvitedAt = DateTime.UtcNow
            });

            // 计算每代人数（几何级数缩放）
            const double branchFactor = 1.82; // 经验值，使 10 代约 500 人
            var denom = Math.Pow(branchFactor, generations) - 1.0;
            var C = targetMembers * (branchFactor - 1.0) / denom;
            var genCounts = new int[generations];
            var total = 0;
            for (int i = 0; i < generations; i++)
            {
                genCounts[i] = Math.Max(1, (int)Math.Round(C * Math.Pow(branchFactor, i)));
                total += genCounts[i];
            }

            // 若四舍五入导致总数不等于目标，按最后一代调整
            if (total < targetMembers)
            {
                genCounts[generations - 1] += (targetMembers - total);
                total = targetMembers;
            }

            var rand = new Random(12345);
            var personsByGen = new List<List<Person>>();
            var marriages = new List<Marriage>();
            var parentChildren = new List<ParentChild>();

            var baseYear = 1800;
            for (int gen = 0; gen < generations; gen++)
            {
                var list = new List<Person>();
                var year = baseYear + gen * 18; // 每代约 18 年
                for (int j = 0; j < genCounts[gen]; j++)
                {
                    var gender = rand.NextDouble() < 0.5 ? "男" : "女";
                    var person = new Person
                    {
                        Id = Guid.NewGuid(),
                        GenealogyId = genealogy.Id,
                        GivenName = $"王{(gen + 1)}代_{j + 1}",
                        Gender = gender,
                        BirthYear = year + rand.Next(0, 6),
                        CreatedAt = DateTime.UtcNow,
                        Bio = null
                    };
                    list.Add(person);
                }

                // 在当前代内部配对产生婚姻（简单按相邻配对）
                for (int k = 0; k + 1 < list.Count; k += 2)
                {
                    var pA = list[k];
                    var pB = list[k + 1];
                    // 强制一男一女优先，如果不是则仍配对
                    if (pA.Gender == pB.Gender)
                    {
                        // 50% 交换性别标签以保证部分不同
                        if (rand.NextDouble() < 0.5) pA.Gender = "男"; else pB.Gender = "女";
                    }

                    marriages.Add(new Marriage
                    {
                        GenealogyId = genealogy.Id,
                        SpouseAId = pA.Id,
                        SpouseBId = pB.Id,
                        MarriedAtYear = (pA.BirthYear ?? baseYear) + 20
                    });
                }

                personsByGen.Add(list);
            }

            // 生成父母-子女关系：第 i 代作为第 i+1 代的父母来源
            for (int gen = 0; gen < generations - 1; gen++)
            {
                var parents = personsByGen[gen];
                var children = personsByGen[gen + 1];
                // 构建父母对列表（使用 marriages within parents generation when possible）
                var parentPairs = new List<(Guid Father, Guid Mother)>();
                // use marriages to form pairs
                var pairs = marriages.Where(m => parents.Any(p => p.Id == m.SpouseAId) && parents.Any(p => p.Id == m.SpouseBId)).ToList();
                foreach (var m in pairs)
                {
                    var pA = parents.First(p => p.Id == m.SpouseAId);
                    var pB = parents.First(p => p.Id == m.SpouseBId);
                    var father = pA.Gender == "男" ? pA : pB;
                    var mother = pA.Gender == "女" ? pA : pB;
                    parentPairs.Add((Father: father.Id, Mother: mother.Id));
                }

                // 若没有足够配对，按顺序把父母按两两分组
                if (parentPairs.Count == 0)
                {
                    for (int i = 0; i + 1 < parents.Count; i += 2)
                    {
                        var pA = parents[i];
                        var pB = parents[i + 1];
                        var father = pA.Gender == "男" ? pA : pB;
                        var mother = pA.Gender == "女" ? pA : pB;
                        parentPairs.Add((father.Id, mother.Id));
                    }
                }

                if (parentPairs.Count == 0 && parents.Count > 0)
                {
                    // fallback: single parent repeated
                    foreach (var p in parents)
                    {
                        parentPairs.Add((p.Id, Guid.Empty));
                    }
                }

                // 分配每个 child 的父母为随机某个 parentPair
                for (int ci = 0; ci < children.Count; ci++)
                {
                    var child = children[ci];
                    var pair = parentPairs[rand.Next(parentPairs.Count)];
                    if (pair.Father != Guid.Empty)
                    {
                        parentChildren.Add(new ParentChild
                        {
                            GenealogyId = genealogy.Id,
                            ParentId = pair.Father,
                            ChildId = child.Id,
                            RelationshipType = "father"
                        });
                    }
                    if (pair.Mother != Guid.Empty)
                    {
                        parentChildren.Add(new ParentChild
                        {
                            GenealogyId = genealogy.Id,
                            ParentId = pair.Mother,
                            ChildId = child.Id,
                            RelationshipType = "mother"
                        });
                    }
                }
            }

            // 保存所有数据
            var toAddPersons = personsByGen.SelectMany(x => x).ToList();
            db.Persons.AddRange(toAddPersons);
            db.Marriages.AddRange(marriages);
            db.ParentChildren.AddRange(parentChildren);
            db.SaveChanges();
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
