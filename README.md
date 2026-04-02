# nh-tool

Herramienta global de CLI en .NET 8 que genera entidades POCO y mapeos **NHibernate Mapping-by-Code** a partir del esquema de una base de datos, similar a `dotnet ef dbcontext scaffold` pero para NHibernate 5+.

---

## Tabla de Contenido

- [Requisitos Previos](#requisitos-previos)
- [Instalacion](#instalacion)
- [Uso Rapido](#uso-rapido)
- [Referencia del Comando](#referencia-del-comando)
- [Proveedores Soportados](#proveedores-soportados)
- [Que se Genera](#que-se-genera)
  - [Entidades POCO](#entidades-poco)
  - [Mapeos Mapping-by-Code](#mapeos-mapping-by-code)
  - [Propiedades de Navegacion](#propiedades-de-navegacion)
  - [NHibernateHelper](#nhibernatehelper)
- [Arquitectura del Proyecto](#arquitectura-del-proyecto)
  - [Estructura de Carpetas](#estructura-de-carpetas)
  - [Diagrama de Flujo](#diagrama-de-flujo)
  - [Componentes Principales](#componentes-principales)
- [Mapeo de Tipos](#mapeo-de-tipos)
  - [Oracle a C#](#oracle-a-c)
  - [SQL Server a C#](#sql-server-a-c)
- [Convenciones de Nombres](#convenciones-de-nombres)
- [Ejemplos Completos](#ejemplos-completos)
  - [SQL Server](#ejemplo-sql-server)
  - [Oracle](#ejemplo-oracle)
  - [Tablas Especificas](#ejemplo-tablas-especificas)
  - [Excluir Tablas](#ejemplo-excluir-tablas)
  - [Dry Run](#ejemplo-dry-run)
- [Dependencias](#dependencias)
- [Limitaciones Conocidas](#limitaciones-conocidas)
- [Roadmap](#roadmap)

---

## Requisitos Previos

| Requisito          | Version minima |
|--------------------|----------------|
| .NET SDK           | 8.0            |
| SQL Server         | 2012+          |
| Oracle Database    | 12c+           |

---

## Instalacion

### Desde NuGet

```bash
dotnet tool install --global nh-tool
```

### Desde codigo fuente

```bash
git clone https://github.com/tu-usuario/nh-tool.git
cd nh-tool/NHTool

# Compilar
dotnet build

# Empaquetar como herramienta global
dotnet pack

# Instalar globalmente
dotnet tool install --global --add-source ./nupkg nh-tool
```

### Verificar instalacion

```bash
nh-tool --version
nh-tool --help
```

### Desinstalar

```bash
dotnet tool uninstall --global nh-tool
```

---

## Uso Rapido

```bash
# Scaffold de TODAS las tablas de SQL Server
nh-tool nh-scaffold "Server=localhost;Database=MyDb;Trusted_Connection=true;TrustServerCertificate=true" sqlserver

# Scaffold de TODAS las tablas de Oracle
nh-tool nh-scaffold "Data Source=myoracle:1521/ORCL;User Id=HR;Password=hr123;" oracle -s HR

# Scaffold de tablas especificas
nh-tool nh-scaffold "Server=.;Database=Northwind;..." sqlserver -t CUSTOMERS,ORDERS,ORDER_DETAILS -o ./Data -n Northwind.Data

# Excluir tablas
nh-tool nh-scaffold "Server=.;Database=MyDb;..." sqlserver -x AUDIT_LOG,TEMP_DATA

# Previsualizar sin escribir archivos
nh-tool nh-scaffold "Server=.;Database=MyDb;..." sqlserver --dry-run

# Sobreescribir archivos existentes
nh-tool nh-scaffold "Server=.;Database=MyDb;..." sqlserver --force
```

---

## Referencia del Comando

```
nh-tool nh-scaffold <connection-string> <provider> [opciones]
```

### Argumentos

| Argumento             | Requerido | Descripcion                                |
|-----------------------|-----------|--------------------------------------------|
| `<connection-string>` | Si        | Cadena de conexion a la base de datos       |
| `<provider>`          | Si        | Proveedor: `oracle`, `sqlserver` o `mssql`  |

### Opciones

| Opcion                    | Alias | Default        | Descripcion                                                             |
|---------------------------|-------|----------------|-------------------------------------------------------------------------|
| `--output <dir>`          | `-o`  | `./Generated`  | Directorio de salida para los archivos generados                        |
| `--namespace <ns>`        | `-n`  | `MyApp.Data`   | Namespace raiz para las clases generadas                                |
| `--schema <schema>`       | `-s`  | `dbo` / auto   | Filtro de schema/owner (ej: `dbo`, `HR`)                                |
| `--tables <lista>`        | `-t`  | *(todas)*       | Lista de tablas separadas por coma (ej: `USERS,ORDERS,ORDER_ITEMS`)     |
| `--exclude-tables <lista>`| `-x`  | *(ninguna)*     | Lista de tablas a excluir separadas por coma (ej: `AUDIT_LOG,TEMP_DATA`)|
| `--use-legacy-style`      | `-l`  | `false`        | Genera codigo compatible con .NET Framework (C# 7.3)                    |
| `--dry-run`               | `-d`  | `false`        | Previsualiza los archivos que se generarian sin escribirlos              |
| `--force`                 | `-f`  | `false`        | Sobreescribe archivos existentes en el directorio de salida             |
| `--help`                  | `-h`  | -              | Muestra la ayuda                                                        |

### Codigos de Salida

| Codigo | Significado                                     |
|--------|-------------------------------------------------|
| `0`    | Scaffold completado correctamente               |
| `1`    | Error de argumento (provider no soportado, etc) |
| `2`    | Error inesperado (conexion fallida, etc)        |

---

## Proveedores Soportados

| Nombre CLI              | Base de Datos | Driver NHibernate                                | Dialecto                              |
|-------------------------|---------------|--------------------------------------------------|---------------------------------------|
| `oracle`                | Oracle 12c+   | `OracleManagedDataClientDriver`                  | `Oracle12cDialect`                    |
| `sqlserver` o `mssql`   | SQL Server    | `MicrosoftDataSqlClientDriver`                   | `MsSql2012Dialect`                    |

---

## Que se Genera

Al ejecutar el scaffold sobre una base de datos, se genera la siguiente estructura de archivos:

```
<output-dir>/
  Entities/
    User.cs              # Clase POCO con propiedades de navegacion
    Order.cs
    OrderItem.cs
  Mappings/
    UserMap.cs           # ClassMapping<T> con ManyToOne/Bag
    OrderMap.cs
    OrderItemMap.cs
  NHibernateHelper.cs    # Configuracion de ISessionFactory
```

### Entidades POCO

Cada tabla genera una clase con propiedades `virtual` (requisito de NHibernate para Lazy Loading):

```csharp
using System;
using System.Collections.Generic;

namespace MyApp.Data;

public class Order
{
    public virtual int Id { get; set; }
    public virtual DateTime OrderDate { get; set; }
    public virtual decimal TotalAmount { get; set; }

    // ManyToOne - FK column (CUSTOMER_ID) replaced by navigation property
    public virtual Customer Customer { get; set; }

    // OneToMany - inverse collection
    public virtual IList<OrderItem> OrderItems { get; set; } = new List<OrderItem>();
}
```

**Caracteristicas:**
- Todas las propiedades son `virtual` para soportar lazy loading via proxies.
- Los tipos nullable de la BD se traducen a tipos nullable de C# (ej: `DateTime?`, `int?`).
- Los tipos referencia (`string`, `byte[]`) no agregan `?` ya que son nullable por defecto.
- Las columnas FK se reemplazan por propiedades de navegacion `ManyToOne`.
- Las relaciones inversas generan colecciones `IList<T>`.

### Mapeos Mapping-by-Code

Cada entidad genera su correspondiente `ClassMapping<T>` usando el API nativo de NHibernate:

```csharp
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;

namespace MyApp.Data.Mappings;

public class OrderMap : ClassMapping<Order>
{
    public OrderMap()
    {
        Schema("dbo");
        Table("ORDERS");

        Id(x => x.Id, m =>
        {
            m.Column("ID");
            m.Generator(Generators.Identity);  // Detected from SQL Server IDENTITY
        });

        Property(x => x.OrderDate, m =>
        {
            m.Column("ORDER_DATE");
            m.NotNullable(true);
        });

        Property(x => x.TotalAmount, m =>
        {
            m.Column("TOTAL_AMOUNT");
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

### Propiedades de Navegacion

La herramienta detecta Foreign Keys automaticamente y genera:

**ManyToOne (lado FK):**
- La columna FK (ej: `CUSTOMER_ID`) se reemplaza por una propiedad de navegacion al tipo referenciado.
- El nombre de la propiedad se deriva de la columna FK quitando sufijos comunes (`_ID`, `_KEY`, `_FK`, `_CODE`).

| Columna FK          | Tabla Referenciada | Propiedad Generada     |
|---------------------|--------------------|------------------------|
| `CUSTOMER_ID`       | `CUSTOMERS`        | `Customer`             |
| `CREATED_BY_USER_ID`| `USERS`            | `CreatedByUser`        |
| `CATEGORY_ID`       | `CATEGORIES`       | `Category`             |

**OneToMany (lado inverso):**
- Se genera una coleccion `IList<T>` en la entidad padre con el nombre plural de la tabla hija.
- El mapeo usa `Bag` con `Inverse(true)` y `Lazy(CollectionLazy.Lazy)`.

| Tabla Hija     | Propiedad Generada en Padre |
|----------------|-----------------------------|
| `ORDER_ITEMS`  | `IList<OrderItem> OrderItems`|
| `ADDRESSES`    | `IList<Address> Addresses`   |

**Mapeo de Primary Keys:**

| Escenario                   | Estrategia                                                                    |
|-----------------------------|-------------------------------------------------------------------------------|
| PK con IDENTITY (SQL Server)| `Generators.Identity`                                                         |
| PK con secuencia (Oracle)   | `Generators.Sequence` con nombre de secuencia                                 |
| PK numerico sin IDENTITY    | `Generators.Native`                                                           |
| PK no numerico (string, Guid)| `Generators.Assigned`                                                        |
| PK compuesta                | `ComposedId(m => { m.Property(...); ... })`                                   |

**Ejemplo Oracle con secuencia:**

```csharp
Id(x => x.Id, m =>
{
    m.Column("ID");
    m.Generator(Generators.Sequence, g => g.Params(new { sequence = "SEQ_EMPLOYEES" }));
});
```

### NHibernateHelper

Se genera un archivo `NHibernateHelper.cs` que configura el `ISessionFactory` con todos los mapeos registrados:

```csharp
using NHibernate;
using NHibernate.Cfg;
using NHibernate.Mapping.ByCode;
using MyApp.Data.Mappings;

namespace MyApp.Data;

public static class NHibernateHelper
{
    private static ISessionFactory? _sessionFactory;

    public static ISessionFactory BuildSessionFactory(string connectionString)
    {
        if (_sessionFactory != null) return _sessionFactory;

        var cfg = new Configuration();
        cfg.DataBaseIntegration(db =>
        {
            db.Dialect<NHibernate.Dialect.MsSql2012Dialect>();
            db.Driver<NHibernate.Driver.MicrosoftDataSqlClientDriver>();
            db.ConnectionString = connectionString;
        });

        var mapper = new ConventionModelMapper();
        mapper.AddMapping<UserMap>();
        mapper.AddMapping<OrderMap>();
        mapper.AddMapping<OrderItemMap>();

        cfg.AddMapping(mapper.CompileMappingForAllExplicitlyAddedEntities());

        _sessionFactory = cfg.BuildSessionFactory();
        return _sessionFactory;
    }
}
```

**Uso en tu aplicacion:**

```csharp
var factory = NHibernateHelper.BuildSessionFactory("Server=.;Database=MyDb;...");

using var session = factory.OpenSession();
using var tx = session.BeginTransaction();

var users = session.Query<User>()
    .Where(u => u.IsActive)
    .ToList();

tx.Commit();
```

---

## Arquitectura del Proyecto

### Estructura de Carpetas

```
nh-tool/
  NHTool/
    NHTool.csproj   # Proyecto .NET 8 (Global Tool)
    Program.cs                      # Entry point - CLI con System.CommandLine

    Models/
      TableInfo.cs                  # Modelo: tabla con schema, nombre, columnas y FKs
      ColumnInfo.cs                 # Modelo: columna con tipo, nullable, PK, Identity, etc.
      ForeignKeyInfo.cs             # Modelo: FK con tablas y columnas origen/destino
      DatabaseProvider.cs           # Enum Oracle|SqlServer + parser de string

    Schema/
      ISchemaReader.cs              # Interfaz: ReadTablesAsync + ReadForeignKeysAsync
      OracleSchemaReader.cs         # ALL_TABLES, ALL_TAB_COLUMNS, ALL_CONSTRAINTS, ALL_TAB_IDENTITY_COLS
      SqlServerSchemaReader.cs      # INFORMATION_SCHEMA + sys.columns (IDENTITY)
      SchemaReaderFactory.cs        # Factory que selecciona reader por provider

    Helpers/
      NamingHelper.cs               # Conversion DB names -> PascalCase + nombres de navegacion
      TypeMapper.cs                 # Conversion DB types -> CLR types

    CodeGen/
      EntityGenerator.cs            # Genera clases POCO con propiedades de navegacion
      MappingGenerator.cs           # Genera ClassMapping<T> con ManyToOne/Bag
      SessionFactoryGenerator.cs    # Genera NHibernateHelper.cs
      ScaffoldOrchestrator.cs       # Orquesta el flujo completo (FK wiring, filtros, etc.)
```

### Diagrama de Flujo

```
 CLI (Program.cs)
  |
  v
 ScaffoldOrchestrator.RunAsync(...)
  |
  |-- 1. SchemaReaderFactory.Create(provider)
  |       |-- OracleSchemaReader     (ALL_TABLES, ALL_TAB_COLUMNS, ALL_CONSTRAINTS, ALL_TAB_IDENTITY_COLS)
  |       '-- SqlServerSchemaReader  (INFORMATION_SCHEMA + sys.columns)
  |
  |-- 2. reader.ReadTablesAsync(connectionString, schema)
  |       '-- Retorna List<TableInfo> con columnas, PKs, Identity/Sequence
  |
  |-- 3. Filtros: --tables (incluir) y --exclude-tables (excluir)
  |
  |-- 4. reader.ReadForeignKeysAsync(connectionString, schema)
  |       '-- Retorna List<ForeignKeyInfo>, se asignan a ForeignKeys/InverseForeignKeys
  |
  |-- 5. EntityGenerator.Generate(table, namespace)
  |       '-- POCO con escalares + ManyToOne + IList<T> collections
  |
  |-- 6. MappingGenerator.Generate(table, namespace)
  |       '-- ClassMapping<T> con Id (Identity/Sequence/Native/Assigned),
  |          Property, ManyToOne, Bag
  |
  '-- 7. SessionFactoryGenerator.Generate(tables, namespace)
          '-- NHibernateHelper.cs con ISessionFactory thread-safe
```

### Componentes Principales

#### `Program.cs` - CLI Engine

Usa `System.CommandLine` con handler basado en `InvocationContext` para soportar mas de 8 parametros. Define el comando `nh-scaffold` con:
- 2 argumentos posicionales: `connection-string` y `provider`
- 8 opciones: `--output`, `--namespace`, `--schema`, `--tables`, `--exclude-tables`, `--use-legacy-style`, `--dry-run`, `--force`

#### `ISchemaReader` - Lectura de Esquema

```csharp
public interface ISchemaReader
{
    Task<List<TableInfo>> ReadTablesAsync(string connectionString, string? schemaFilter = null);
    Task<List<ForeignKeyInfo>> ReadForeignKeysAsync(string connectionString, string? schemaFilter = null);
}
```

| Implementacion           | Origen de Datos                                                        |
|--------------------------|------------------------------------------------------------------------|
| `OracleSchemaReader`     | `ALL_TABLES`, `ALL_TAB_COLUMNS`, `ALL_CONSTRAINTS`, `ALL_TAB_IDENTITY_COLS` |
| `SqlServerSchemaReader`  | `INFORMATION_SCHEMA.TABLES`, `.COLUMNS`, `.KEY_COLUMN_USAGE`, `sys.columns` |

Ambos readers:
1. Listan todas las tablas del schema en una sola query.
2. Leen todas las columnas con tipo, nullability, longitud y PKs en una sola round-trip.
3. Detectan columnas IDENTITY (SQL Server) o Identity con secuencias (Oracle 12c+).
4. Leen Foreign Keys con tablas/columnas origen y destino.

#### `NamingHelper` - Pluralizacion y Formato

Usa la libreria **Humanizer** para:

| Entrada DB              | Metodo                     | Salida C#          |
|-------------------------|----------------------------|--------------------|
| `USERS`                 | `ToClassName`              | `User`             |
| `ORDER_ITEMS`           | `ToClassName`              | `OrderItem`        |
| `USER_NAME`             | `ToPropertyName`           | `UserName`         |
| `CUSTOMER_ID`           | `ToManyToOnePropertyName`  | `Customer`         |
| `CREATED_BY_USER_ID`    | `ToManyToOnePropertyName`  | `CreatedByUser`    |
| `ORDER_ITEMS`           | `ToCollectionPropertyName` | `OrderItems`       |

#### `TypeMapper` - Mapeo de Tipos de Datos

Mapea los tipos de columna de la BD al tipo CLR mas apropiado, considerando:
- **Precision y escala** para tipos numericos (Oracle `NUMBER`).
- **Nullability**: agrega `?` a value types cuando la columna admite nulos.
- **NRT-safe**: no agrega `?` a tipos referencia para evitar CS8632 en proyectos sin NRT.

---

## Mapeo de Tipos

### Oracle a C#

| Tipo Oracle                | Condicion                               | Tipo C#     |
|----------------------------|-----------------------------------------|-------------|
| `NUMBER`                   | Sin precision/escala                     | `int`       |
| `NUMBER`                   | `Scale=0`, `Precision<=10`              | `int`       |
| `NUMBER`                   | `Scale=0`, `Precision>10`               | `long`      |
| `NUMBER`                   | Otro caso                                | `decimal`   |
| `FLOAT`, `BINARY_FLOAT`   | -                                       | `float`     |
| `BINARY_DOUBLE`            | -                                       | `double`    |
| `VARCHAR2`, `NVARCHAR2`    | -                                       | `string`    |
| `CHAR`, `NCHAR`            | -                                       | `string`    |
| `CLOB`, `NCLOB`            | -                                       | `string`    |
| `DATE`                     | -                                       | `DateTime`  |
| `TIMESTAMP*`               | -                                       | `DateTime`  |
| `BLOB`, `RAW`, `LONG RAW`  | -                                       | `byte[]`    |
| `XMLTYPE`                  | -                                       | `string`    |

### SQL Server a C#

| Tipo SQL Server             | Tipo C#          |
|-----------------------------|------------------|
| `int`                       | `int`            |
| `bigint`                    | `long`           |
| `smallint`                  | `short`          |
| `tinyint`                   | `byte`           |
| `bit`                       | `bool`           |
| `decimal`, `numeric`        | `decimal`        |
| `money`, `smallmoney`       | `decimal`        |
| `float`                     | `double`         |
| `real`                      | `float`          |
| `varchar`, `nvarchar`       | `string`         |
| `char`, `nchar`             | `string`         |
| `text`, `ntext`, `xml`      | `string`         |
| `date`, `datetime`          | `DateTime`       |
| `datetime2`, `smalldatetime`| `DateTime`       |
| `datetimeoffset`            | `DateTimeOffset` |
| `time`                      | `TimeSpan`       |
| `uniqueidentifier`          | `Guid`           |
| `varbinary`, `binary`       | `byte[]`         |
| `image`, `timestamp`        | `byte[]`         |

> Los value types (`int`, `DateTime`, `Guid`, etc.) se generan como nullable (`int?`, `DateTime?`) cuando la columna admite `NULL`.

---

## Convenciones de Nombres

| Elemento              | Convencion                                    | Ejemplo                            |
|-----------------------|-----------------------------------------------|------------------------------------|
| Nombre de clase       | PascalCase, singular                           | `USERS` -> `User`                 |
| Nombre de archivo     | `{ClassName}.cs`                               | `User.cs`                         |
| Propiedades escalares | PascalCase                                     | `FIRST_NAME` -> `FirstName`       |
| Propiedad ManyToOne   | FK sin sufijo `_ID/_KEY/_FK/_CODE`             | `CUSTOMER_ID` -> `Customer`       |
| Propiedad coleccion   | PascalCase plural                              | `ORDER_ITEMS` -> `OrderItems`     |
| Mapeos                | `{ClassName}Map.cs`                            | `UserMap.cs`                      |
| Namespace entidad     | `<namespace>`                                  | `MyApp.Data`                      |
| Namespace mapeo       | `<namespace>.Mappings`                         | `MyApp.Data.Mappings`             |
| Carpeta entidades     | `<output>/Entities/`                           | `./Generated/Entities/`           |
| Carpeta mapeos        | `<output>/Mappings/`                           | `./Generated/Mappings/`           |

---

## Ejemplos Completos

### Ejemplo SQL Server

```bash
nh-tool nh-scaffold \
  "Server=localhost;Database=Northwind;Trusted_Connection=true;TrustServerCertificate=true" \
  sqlserver \
  -o ./Northwind.Data/Generated \
  -n Northwind.Data \
  -s dbo
```

Salida:

```
Connecting to SqlServer database...
Found 13 table(s). Generating code...
  -> ./Northwind.Data/Generated/Entities/Category.cs
  -> ./Northwind.Data/Generated/Mappings/CategoryMap.cs
  -> ./Northwind.Data/Generated/Entities/Customer.cs
  -> ./Northwind.Data/Generated/Mappings/CustomerMap.cs
  ...
  -> ./Northwind.Data/Generated/NHibernateHelper.cs

Scaffold complete! 13 entities generated in './Northwind.Data/Generated'.
```

### Ejemplo Oracle

```bash
nh-tool nh-scaffold \
  "Data Source=(DESCRIPTION=(ADDRESS=(PROTOCOL=TCP)(HOST=dbserver)(PORT=1521))(CONNECT_DATA=(SERVICE_NAME=ORCL)));User Id=HR;Password=hr123;" \
  oracle \
  -o ./HrModule/Generated \
  -n HrModule.Data \
  -s HR
```

### Ejemplo Tablas Especificas

Generar solo las tablas `EMPLOYEES`, `DEPARTMENTS` y `JOB_HISTORY`:

```bash
nh-tool nh-scaffold \
  "Data Source=myoracle:1521/ORCL;User Id=HR;Password=hr123;" \
  oracle \
  -t EMPLOYEES,DEPARTMENTS,JOB_HISTORY \
  -o ./Output \
  -n HrModule.Data \
  -s HR
```

Salida:

```
Connecting to Oracle database...
Filtered 27 -> 3 table(s) by --tables parameter.
Found 3 table(s). Generating code...
  -> ./Output/Entities/Employee.cs
  -> ./Output/Mappings/EmployeeMap.cs
  -> ./Output/Entities/Department.cs
  -> ./Output/Mappings/DepartmentMap.cs
  -> ./Output/Entities/JobHistory.cs
  -> ./Output/Mappings/JobHistoryMap.cs
  -> ./Output/NHibernateHelper.cs

Scaffold complete! 3 entities generated in './Output'.
```

### Ejemplo Excluir Tablas

Excluir tablas de auditoria y temporales:

```bash
nh-tool nh-scaffold \
  "Server=.;Database=MyDb;Trusted_Connection=true;TrustServerCertificate=true" \
  sqlserver \
  -x AUDIT_LOG,TEMP_DATA,MIGRATION_HISTORY \
  -o ./Generated \
  -n MyApp.Data
```

Salida:

```
Connecting to SqlServer database...
Excluded 3 table(s) by --exclude-tables parameter.
Found 25 table(s). Generating code...
  ...
Scaffold complete! 25 entities generated in './Generated'.
```

### Ejemplo Dry Run

Previsualizar los archivos que se generarian:

```bash
nh-tool nh-scaffold \
  "Server=.;Database=MyDb;..." sqlserver \
  --dry-run -t USERS,ORDERS
```

Salida:

```
Connecting to SqlServer database...
Filtered 28 -> 2 table(s) by --tables parameter.
Found 2 table(s). Generating code...

[DRY RUN] The following files would be generated:
  -> ./Generated/Entities/User.cs
  -> ./Generated/Mappings/UserMap.cs
  -> ./Generated/Entities/Order.cs
  -> ./Generated/Mappings/OrderMap.cs
  -> ./Generated/NHibernateHelper.cs

[DRY RUN] 2 entities would be generated. No files were written.
```

---

## Dependencias

| Paquete                            | Version               | Proposito                                    |
|------------------------------------|-----------------------|----------------------------------------------|
| `System.CommandLine`               | 2.0.0-beta4.22272.1   | Motor de CLI (parsing de comandos/opciones)  |
| `NHibernate`                       | 5.5.2                 | ORM - usado para los tipos de Mapping-by-Code|
| `Oracle.ManagedDataAccess.Core`    | 23.4.0                | Conectividad con Oracle Database             |
| `Microsoft.Data.SqlClient`         | 5.2.1                 | Conectividad con SQL Server                  |
| `Humanizer.Core`                   | 2.14.1                | Pluralizacion y PascalCase                   |

---

## Limitaciones Conocidas

- **No genera indices ni unique constraints.**
- **No soporta vistas**, solo tablas base (`BASE TABLE` / `ALL_TABLES`).
- **No soporta herencia de tablas** (Table-per-hierarchy, Table-per-class, etc.).
- **No FluentNHibernate**: todo el mapeo es con el API nativo `Mapping.ByCode` de NHibernate 5+.
- **FKs entre schemas distintos** no se resuelven (ambas tablas deben estar en el mismo schema filtrado).

---

## Roadmap

- [x] Soporte para **Foreign Keys** y generacion de propiedades de navegacion (`ManyToOne`, `OneToMany`).
- [x] Deteccion de **secuencias Oracle** y generacion de `Generators.Sequence("SEQ_NAME")`.
- [x] Deteccion de **IDENTITY** en SQL Server y generacion de `Generators.Identity`.
- [x] Opcion `--exclude-tables` para excluir tablas especificas.
- [x] Modo `--dry-run` para previsualizar sin escribir archivos.
- [x] Opcion `--force` para sobreescribir archivos existentes.
- [x] Modo `--use-legacy-style` para compatibilidad con .NET Framework (C# 7.3).
- [ ] Soporte para **PostgreSQL** (`Npgsql`).
- [ ] Soporte para **MySQL/MariaDB**.
- [ ] Generacion de **unit tests** base para validar los mapeos.
