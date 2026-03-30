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
    User.cs              # Clase POCO
    Order.cs
    OrderItem.cs
  Mappings/
    UserMap.cs           # ClassMapping<T> (Mapping-by-Code)
    OrderMap.cs
    OrderItemMap.cs
  NHibernateHelper.cs    # Configuracion de ISessionFactory
```

### Entidades POCO

Cada tabla genera una clase con propiedades `virtual` (requisito de NHibernate para Lazy Loading):

```csharp
using System;

namespace MyApp.Data;

public class User
{
    public virtual int Id { get; set; }
    public virtual string FirstName { get; set; }
    public virtual string LastName { get; set; }
    public virtual string Email { get; set; }
    public virtual DateTime? CreatedAt { get; set; }
    public virtual bool IsActive { get; set; }
}
```

**Caracteristicas:**
- Todas las propiedades son `virtual` para soportar lazy loading via proxies.
- Los tipos nullable de la BD se traducen a tipos nullable de C# (ej: `DateTime?`, `int?`).
- Los tipos referencia (`string`, `byte[]`) no agregan `?` ya que son nullable por defecto.

### Mapeos Mapping-by-Code

Cada entidad genera su correspondiente `ClassMapping<T>` usando el API nativo de NHibernate:

```csharp
using NHibernate.Mapping.ByCode;
using NHibernate.Mapping.ByCode.Conformist;

namespace MyApp.Data.Mappings;

public class UserMap : ClassMapping<User>
{
    public UserMap()
    {
        Schema("dbo");
        Table("USERS");

        Id(x => x.Id, m =>
        {
            m.Column("ID");
            m.Generator(Generators.Native);
        });

        Property(x => x.FirstName, m =>
        {
            m.Column("FIRST_NAME");
            m.NotNullable(true);
            m.Length(100);
        });

        Property(x => x.Email, m =>
        {
            m.Column("EMAIL");
            m.Length(255);
        });

        Property(x => x.CreatedAt, m =>
        {
            m.Column("CREATED_AT");
        });
    }
}
```

**Mapeo de Primary Keys:**

| Escenario        | Estrategia                                        |
|------------------|---------------------------------------------------|
| PK simple        | `Id(x => x.Prop, m => { m.Generator(Native); })` |
| PK compuesta     | `ComposedId(m => { m.Property(...); ... })`       |
| Sin PK           | No genera bloque de Id                             |

**Ejemplo de PK compuesta:**

```csharp
// Tabla ORDER_ITEMS con PK compuesta (ORDER_ID, PRODUCT_ID)
ComposedId(m =>
{
    m.Property(x => x.OrderId, p => p.Column("ORDER_ID"));
    m.Property(x => x.ProductId, p => p.Column("PRODUCT_ID"));
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
      TableInfo.cs                  # Modelo: tabla con schema, nombre y columnas
      ColumnInfo.cs                 # Modelo: columna con tipo, nullable, PK, etc.
      DatabaseProvider.cs           # Enum Oracle|SqlServer + parser de string

    Schema/
      ISchemaReader.cs              # Interfaz del lector de esquema
      OracleSchemaReader.cs         # Implementacion: ALL_TABLES + ALL_TAB_COLUMNS
      SqlServerSchemaReader.cs      # Implementacion: INFORMATION_SCHEMA
      SchemaReaderFactory.cs        # Factory que selecciona reader por provider

    Helpers/
      NamingHelper.cs               # Conversion DB names -> PascalCase (Humanizer)
      TypeMapper.cs                 # Conversion DB types -> CLR types

    CodeGen/
      EntityGenerator.cs            # Genera clases POCO (.cs)
      MappingGenerator.cs           # Genera ClassMapping<T> (.cs)
      SessionFactoryGenerator.cs    # Genera NHibernateHelper.cs
      ScaffoldOrchestrator.cs       # Orquesta el flujo completo
```

### Diagrama de Flujo

```
 CLI (Program.cs)
  |
  v
 ScaffoldOrchestrator.RunAsync(...)
  |
  |-- 1. SchemaReaderFactory.Create(provider)
  |       |-- OracleSchemaReader     (ALL_TABLES, ALL_TAB_COLUMNS, ALL_CONSTRAINTS)
  |       '-- SqlServerSchemaReader  (INFORMATION_SCHEMA.TABLES, .COLUMNS, .KEY_COLUMN_USAGE)
  |
  |-- 2. reader.ReadTablesAsync(connectionString, schema)
  |       '-- Retorna List<TableInfo> con columnas y PKs
  |
  |-- 3. Filtro por --tables (si se especifica)
  |       '-- Interseccion case-insensitive + warnings de tablas no encontradas
  |
  |-- 4. EntityGenerator.Generate(table, namespace)
  |       '-- Genera POCO con propiedades virtual
  |
  |-- 5. MappingGenerator.Generate(table, namespace)
  |       '-- Genera ClassMapping<T> con Id/ComposedId/Property
  |
  '-- 6. SessionFactoryGenerator.Generate(tables, namespace)
          '-- Genera NHibernateHelper.cs con ISessionFactory
```

### Componentes Principales

#### `Program.cs` - CLI Engine

Usa `System.CommandLine` para parsear argumentos y opciones. Define el comando `nh-scaffold` con:
- 2 argumentos posicionales: `connection-string` y `provider`
- 4 opciones: `--output`, `--namespace`, `--schema`, `--tables`

#### `ISchemaReader` - Lectura de Esquema

```csharp
public interface ISchemaReader
{
    Task<List<TableInfo>> ReadTablesAsync(string connectionString, string? schemaFilter = null);
}
```

| Implementacion           | Origen de Datos                                              |
|--------------------------|--------------------------------------------------------------|
| `OracleSchemaReader`     | `ALL_TABLES`, `ALL_TAB_COLUMNS`, `ALL_CONSTRAINTS`           |
| `SqlServerSchemaReader`  | `INFORMATION_SCHEMA.TABLES`, `.COLUMNS`, `.KEY_COLUMN_USAGE` |

Ambos readers:
1. Listan todas las tablas del schema.
2. Para cada tabla, consultan las columnas con tipo, nullability y longitud.
3. Detectan Primary Keys mediante joins a las tablas de constraints.

#### `NamingHelper` - Pluralizacion y Formato

Usa la libreria **Humanizer** para:

| Entrada DB        | Metodo          | Salida C#      |
|-------------------|-----------------|----------------|
| `USERS`           | `ToClassName`   | `User`         |
| `ORDER_ITEMS`     | `ToClassName`   | `OrderItem`    |
| `PRODUCT`         | `ToClassName`   | `Product`      |
| `USER_NAME`       | `ToPropertyName`| `UserName`     |
| `ID`              | `ToPropertyName`| `Id`           |
| `CREATED_AT`      | `ToPropertyName`| `CreatedAt`    |

El proceso:
1. Convierte a minusculas: `ORDER_ITEMS` -> `order_items`
2. Reemplaza `_` por espacios: `order items`
3. Pascaliza: `OrderItems`
4. Singulariza: `OrderItem`

#### `TypeMapper` - Mapeo de Tipos de Datos

Mapea los tipos de columna de la BD al tipo CLR mas apropiado, considerando:
- **Precision y escala** para tipos numericos (Oracle `NUMBER`).
- **Nullability**: agrega `?` a value types cuando la columna admite nulos.

---

## Mapeo de Tipos

### Oracle a C#

| Tipo Oracle                | Condicion                               | Tipo C#     |
|----------------------------|-----------------------------------------|-------------|
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

| Elemento          | Convencion                                    | Ejemplo                          |
|-------------------|-----------------------------------------------|----------------------------------|
| Nombre de clase   | PascalCase, singular                           | `USERS` -> `User`               |
| Nombre de archivo | `{ClassName}.cs`                               | `User.cs`                       |
| Propiedades       | PascalCase                                     | `FIRST_NAME` -> `FirstName`     |
| Mapeos            | `{ClassName}Map.cs`                            | `UserMap.cs`                    |
| Namespace entidad | `<namespace>`                                  | `MyApp.Data`                    |
| Namespace mapeo   | `<namespace>.Mappings`                         | `MyApp.Data.Mappings`           |
| Carpeta entidades | `<output>/Entities/`                           | `./Generated/Entities/`         |
| Carpeta mapeos    | `<output>/Mappings/`                           | `./Generated/Mappings/`         |

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

Si alguna tabla no existe, se muestra un warning:

```
Warning: tables not found in schema: NONEXISTENT_TABLE
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

- **No genera relaciones (FK):** No se crean propiedades de navegacion (`ManyToOne`, `Bag`, `Set`). Solo se mapean columnas escalares.
- **No genera indices ni unique constraints.**
- **No soporta vistas**, solo tablas base (`BASE TABLE` / `ALL_TABLES`).
- **No soporta herencia de tablas** (Table-per-hierarchy, Table-per-class, etc.).
- **Generador de PK fijo en `Native`**: no detecta secuencias Oracle ni columnas `IDENTITY` de SQL Server automaticamente.
- **No FluentNHibernate**: todo el mapeo es con el API nativo `Mapping.ByCode` de NHibernate 5+.

---

## Roadmap

- [ ] Soporte para **Foreign Keys** y generacion de propiedades de navegacion (`ManyToOne`, `OneToMany`).
- [ ] Deteccion de **secuencias Oracle** y generacion de `Generators.Sequence("SEQ_NAME")`.
- [ ] Deteccion de **IDENTITY** en SQL Server y generacion de `Generators.Identity`.
- [ ] Soporte para **PostgreSQL** (`Npgsql`).
- [ ] Soporte para **MySQL/MariaDB**.
- [ ] Opcion `--force` para sobreescribir archivos existentes con confirmacion.
- [ ] Opcion `--exclude-tables` para excluir tablas especificas.
- [ ] Generacion de **unit tests** base para validar los mapeos.
- [ ] Modo `--dry-run` para previsualizar sin escribir archivos.
