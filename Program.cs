using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace ef_core_clear
{
    class Program
    {
        static void Main(string[] args)
        {
            using (var appDbContext = new AppDbContext())
            {
                appDbContext.Database.EnsureDeleted();
                appDbContext.Database.EnsureCreated();
                Console.WriteLine("The database has been reset.");

                appDbContext.Users.Add(new User() {
                    Name = "John Doe",
                    Tags = new[] {
                        new Tag() { Name = "A" },
                        new Tag() { Name = "B" },
                        new Tag() { Name = "C" },
                    },
                });

                appDbContext.SaveChanges();
            }

            using (var appDbContext = new AppDbContext())
            {
                foreach (var user in appDbContext.Users.Include(u => u.Tags).ToArray())
                {
                    Console.WriteLine($"{user.Name} ({user.Id})");
                    foreach (var tag in user.Tags)
                    {
                        Console.WriteLine($" - {tag.Name} ({tag.Id})");
                    }
                }
            }

            using (var appDbContext = new AppDbContext())
            {
                //var user = appDbContext.Users.Include(u => u.Tags).Single();
                //user.Tags.Clear();
                var user = appDbContext.Users.Single();
                user.Tags = new Tag[] {};
                appDbContext.SaveChanges();
            }

            using (var appDbContext = new AppDbContext())
            {
                foreach (var user in appDbContext.Users.Include(u => u.Tags).ToArray())
                {
                    Console.WriteLine($"{user.Name} ({user.Id})");
                    foreach (var tag in user.Tags)
                    {
                        Console.WriteLine($" - {tag.Name} ({tag.Id})");
                    }
                }
            }
        }
    }

    public class User
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public ICollection<Tag> Tags { get; set; }
    }

    public class Tag
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public int UserId { get; set; }
        public User User { get; set; }
    }

    public class AppDbContext: DbContext
    {
        public DbSet<User> Users { get; set; }
        public DbSet<Tag> Tags { get; set; }
        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            optionsBuilder.UseSqlServer($@"Server=(localdb)\{nameof(ef_core_clear)};Database={nameof(ef_core_clear)};");
        }
    }
}
