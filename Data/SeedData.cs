using GenealogyApp.Models;

namespace GenealogyApp.Data
{
    public static class SeedData
    {
        public static void EnsureSeeded(ApplicationDbContext db)
        {
            if (db.Genealogies.Any())
            {
                NormalizeSampleData(db);
                return;
            }

            var genealogy = new Genealogy
            {
                Id = Guid.NewGuid(),
                Title = "张氏族谱",
                Surname = "张",
                CompiledAt = DateTime.UtcNow,
                CreatedByUserId = Guid.Empty,
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
    }
}
