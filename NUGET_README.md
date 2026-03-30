# nh-tool

CLI tool that scaffolds **NHibernate Mapping-by-Code** entities and mappings from your database schema. Think `dotnet ef dbcontext scaffold`, but for NHibernate 5+.

## Install

```bash
dotnet tool install --global nh-tool
```

## Usage

```bash
nh-tool nh-scaffold <connection-string> <provider> [options]
```

### Quick Examples

```bash
# SQL Server - all tables
nh-tool nh-scaffold "Server=.;Database=MyDb;Trusted_Connection=true;TrustServerCertificate=true" sqlserver \
  -o ./Generated -n MyApp.Data

# Oracle - specific tables only
nh-tool nh-scaffold "Data Source=myhost:1521/ORCL;User Id=HR;Password=hr123;" oracle \
  -t EMPLOYEES,DEPARTMENTS -o ./Generated -n HrModule.Data -s HR
```

### Options

| Option | Alias | Default | Description |
|--------|-------|---------|-------------|
| `--output` | `-o` | `./Generated` | Output directory |
| `--namespace` | `-n` | `MyApp.Data` | Root namespace |
| `--schema` | `-s` | `dbo` / connected user | Schema or owner filter |
| `--tables` | `-t` | *(all)* | Comma-separated table list |

## What Gets Generated

```
Generated/
  Entities/
    User.cs           # POCO with virtual properties (lazy loading ready)
    Order.cs
  Mappings/
    UserMap.cs         # ClassMapping<T> (NHibernate Mapping-by-Code)
    OrderMap.cs
  NHibernateHelper.cs  # Thread-safe ISessionFactory configuration
```

### Entity (POCO)

```csharp
public class User
{
    public virtual int Id { get; set; }
    public virtual string FirstName { get; set; } = string.Empty;
    public virtual string Email { get; set; } = string.Empty;
    public virtual DateTime? CreatedAt { get; set; }
}
```

### Mapping (Mapping-by-Code)

```csharp
public class UserMap : ClassMapping<User>
{
    public UserMap()
    {
        Schema("dbo");
        Table("USERS");

        Id(x => x.Id, m =>
        {
            m.Column("ID");
            m.Generator(Generators.Native);  // Assigned for non-numeric PKs
        });

        Property(x => x.FirstName, m =>
        {
            m.Column("FIRST_NAME");
            m.NotNullable(true);
            m.Length(100);
        });
    }
}
```

### SessionFactory Helper

```csharp
var factory = NHibernateHelper.BuildSessionFactory("Server=.;Database=MyDb;...");
using var session = factory.OpenSession();
```

## Supported Databases

| Provider | CLI name | Driver | Dialect |
|----------|----------|--------|---------|
| SQL Server 2012+ | `sqlserver` or `mssql` | `MicrosoftDataSqlClientDriver` | `MsSql2012Dialect` |
| Oracle 12c+ | `oracle` | `OracleManagedDataClientDriver` | `Oracle12cDialect` |

## Key Features

- **Mapping-by-Code nativo** - No FluentNHibernate, uses NHibernate 5+ `ClassMapping<T>`
- **Virtual properties** - Lazy loading ready out of the box
- **Smart naming** - `USERS` -> `User`, `ORDER_ITEMS` -> `OrderItem` (Humanizer)
- **Composite PKs** - Detected and mapped with `ComposedId`
- **PK strategy** - `Generators.Native` for numeric, `Generators.Assigned` for string/Guid
- **Thread-safe** - Generated `NHibernateHelper` uses double-check locking
- **Table filtering** - Scaffold only the tables you need with `--tables`

## Requirements

- .NET 8.0+

## License

MIT
