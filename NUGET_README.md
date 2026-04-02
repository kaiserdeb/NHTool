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

# Exclude tables + force overwrite
nh-tool nh-scaffold "Server=.;Database=MyDb;..." sqlserver \
  -x AUDIT_LOG,TEMP_DATA --force -o ./Generated -n MyApp.Data

# Preview without writing files
nh-tool nh-scaffold "Server=.;Database=MyDb;..." sqlserver --dry-run
```

### Options

| Option | Alias | Default | Description |
|--------|-------|---------|-------------|
| `--output` | `-o` | `./Generated` | Output directory |
| `--namespace` | `-n` | `MyApp.Data` | Root namespace |
| `--schema` | `-s` | `dbo` / connected user | Schema or owner filter |
| `--tables` | `-t` | *(all)* | Comma-separated table include list |
| `--exclude-tables` | `-x` | *(none)* | Comma-separated table exclude list |
| `--use-legacy-style` | `-l` | `false` | .NET Framework compatible output (C# 7.3) |
| `--dry-run` | `-d` | `false` | Preview files without writing |
| `--force` | `-f` | `false` | Overwrite existing files |

## What Gets Generated

```
Generated/
  Entities/
    User.cs           # POCO with virtual properties + navigation
    Order.cs
    OrderItem.cs
  Mappings/
    UserMap.cs         # ClassMapping<T> with ManyToOne/Bag
    OrderMap.cs
    OrderItemMap.cs
  NHibernateHelper.cs  # Thread-safe ISessionFactory configuration
```

### Entity with Navigation Properties

```csharp
public class Order
{
    public virtual int Id { get; set; }
    public virtual DateTime OrderDate { get; set; }
    public virtual decimal TotalAmount { get; set; }

    public virtual Customer Customer { get; set; }                              // ManyToOne
    public virtual IList<OrderItem> OrderItems { get; set; } = new List<OrderItem>();  // OneToMany
}
```

### Mapping with FK + Identity Support

```csharp
public class OrderMap : ClassMapping<Order>
{
    public OrderMap()
    {
        Schema("dbo");
        Table("ORDERS");

        Id(x => x.Id, m =>
        {
            m.Column("ID");
            m.Generator(Generators.Identity);  // Auto-detected from SQL Server IDENTITY
        });

        Property(x => x.OrderDate, m =>
        {
            m.Column("ORDER_DATE");
            m.NotNullable(true);
        });

        ManyToOne(x => x.Customer, m =>
        {
            m.Column("CUSTOMER_ID");
        });

        Bag(x => x.OrderItems, c =>
        {
            c.Key(k => k.Column("ORDER_ID"));
            c.Inverse(true);
            c.Lazy(CollectionLazy.Lazy);
        }, r => r.OneToMany());
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

- **Foreign Keys** - Auto-detected ManyToOne + OneToMany navigation properties
- **IDENTITY / Sequence** - SQL Server IDENTITY and Oracle sequences auto-detected
- **Mapping-by-Code nativo** - No FluentNHibernate, uses NHibernate 5+ `ClassMapping<T>`
- **Smart naming** - `USERS` -> `User`, `CUSTOMER_ID` -> `Customer` navigation (Humanizer)
- **Composite PKs** - Detected and mapped with `ComposedId`
- **PK strategy** - `Identity`, `Sequence`, `Native`, or `Assigned` based on column metadata
- **Thread-safe** - Generated `NHibernateHelper` uses double-check locking
- **Table filtering** - `--tables` to include, `--exclude-tables` to exclude
- **Dry run** - Preview generated files before writing
- **Force overwrite** - `--force` to overwrite existing files
- **Legacy mode** - `--use-legacy-style` for .NET Framework / C# 7.3 compatibility

## Requirements

- .NET 8.0+

## License

MIT
