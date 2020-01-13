# EF Core Clear

This is a repository exploring how to clear a 1:N entity's children in EF and
replace them with a new set of children.

We'll start with an empty application, a console one suffices for the tutorial.

```powershell
dotnet new console
```

Let's ignore `bin` and `obj` for Git:

`.gitignore`
```ini
# .NET Core
bin
obj
```

We'll add EF Core & SQL Server support as a NuGet a package dependency next:

```powershell
dotnet add package Microsoft.EntityFrameworkCore
dotnet add package Microsoft.EntityFrameworkCore.SqlServer
```

Make sure you are using the right NuGet package source
(`https://api.nuget.org/v3/index.json`) if you have other NuGet sources
configured. You can find the configured NuGet sources in Visual Studio > Tools >
Options > NuGet Package Manager > Package Sources. It is sufficient to disable
3rd party sources while you are installing the above packages if you get
problems with them enabled. Please note that the `--source` (`-s`) switch alone
does not suffice.

We're adding SQL Server support because we are going to be using LocalDB.

A quick inspection of `Program.cs` shows that the generated applicaton
namespace is `ef_core_clear` because of the repository directory name
`ef-core-clear`.

Let's create a database by the same name so that we can use `nameof` with it.

```powershell
sqllocaldb create ef_core_clear -s
```

We are not going to add a dependency for `Microsoft.EntityFrameworkCore.Design`
because we are not going to be using the `dotnet ef` tool. This demonstration
is not going to use EF Core Migrations.

Lets set up our context and model classes now:

The `User` entity is the 1 in the 1:N relationship. It's `Tags` property
represents the set we are looking to clear/replace.

```csharp
public class User
{
    public int Id { get; set; }
    public string Name { get; set; }
    public ICollection<Tag> Tags { get; set; }
}
```

The `Tag` entity is the N in the 1:N relationship. We are not looking to
preserve these as we update user. Let's assume a FE application sends a set of
new tags every time the user is updated, instead of a diff of changes (so that
we could know what to keep and changed and delete). The easiest way for us to
update the set is to clear it and recreate it. That's what we're after.

```csharp
public class Tag
{
    public int Id { get; set; }
    public string Name { get; set; }
    public int UserId { get; set; }
    public User User { get; set; }
}
```

We use the convention-named `UserId` and `User` properties to make sure EF Core
spots that there is the 1:N relationship between the User and Tag entities.

Next we add the `using System.Collections.Generic;` namespace import and that's
it for out model classes.

The context class is also straightforward, first we add the EF Core namespace:

`using Microsoft.EntityFrameworkCore;`

Next we wire up the context class and the database connection:

```csharp
public class AppDbContext: DbContext
{
    public DbSet<User> Users { get; set; }
    public DbSet<Tag> Tags { get; set; }
    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
    {
        optionsBuilder.UseSqlServer($@"Server=(localdb)\{nameof(ef_core_clear)};Database={nameof(ef_core_clear)};");
    }
}
```

We're using `nameof` here as we're named the database after our application's
namespace.

Now's the time to try the application out to see if it works.

```powershell
dotnet watch run
```

You should find *Hello World!* printed out in your console now.

We want to make sure the results of out exploration are repeatable, so we make
sure to reset the database between each run.

```csharp
static void Main(string[] args)
{
    using (var appDbContext = new AppDbContext())
    {
        appDbContext.Database.EnsureDeleted();
        appDbContext.Database.EnsureCreated();
        Console.WriteLine("The database has been reset.");
    }
}
```

This should now print only *The database has been reset* - assuming you have
`dotnet watch run` still running.

Next step is to create an user and associate some tags to them.

```csharp
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
        }
```

And while we're at it, let's check out user is really getting created and
receives the correct tags - do this in a new context to make sure the change
tracker state is reset.

We need to import `using System.Linq;` for this.

```csharp
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
}
```

With setup out of the way, now is the time to try to clear the collection of
children. Let's do the naive way first. And again, let's do this in a new DB
context so that change tracker doesn't skew results in any way.

```csharp
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
        var user = appDbContext.Users.Single();
        user.Tags.Clear();
        appDbContext.SaveChanges();
    }
}
```

This won't work because the navigation property `Tags` does not get populated
at all without the `Include`. Let's add it, then:

`appDbContext.Users.Include(u => u.Tags).Single();`

And also duplicate the block which prints the database contents after this one.

Saving, we see that the standard output says:

```
John Doe (1)
 - A (1)
 - B (2)
 - C (3)
John Doe (1)
```

This means success, EF Core correctly cleared the collection.

We did however have to pull the entities into the memory for them to get
removed.

But can we managed the same without pulling the entities into the memory using
`Include`? The collection in the navigation property `Tags` will not get
instantiated unless we use `Include`, but maybe we can set an empty collection
to that navigation property and EF could see that the collection is empty and
drop the dependents for us now?

Let's try:

```csharp
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
```

This didn't work.

So either we load the dependents in memory so that EF Core is aware of all the
relationships as it has them spelled out or we drop down to SQL at which point
we lose the ability to use the same code with the in-memory provider.

## To-Do
