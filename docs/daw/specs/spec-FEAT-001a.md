# Spec FEAT-001a: Acceso con contraseña y configuración del negocio

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001a |
| PRD | docs/daw/prd/prd-FEAT-001a.md |
| Tier | FEATURE |
| Fecha | 2026-10-16 |
| Spec loops | 1 |
| Threat model | docs/daw/security/threat-FEAT-001a.md |
| ADR | docs/adr/adr-001-decimales-como-enteros-escalados.md |

## Resumen
Se crea la solución .NET 10 como monolito modular en cuatro capas (`Optica.Domain`, `Optica.Application`, `Optica.Infrastructure`, `Optica.Api`) y el SPA React 19 + Vite en `web/`. El acceso usa una única contraseña guardada como hash PBKDF2 en una fila única de SQLite, sesión por cookie validada en cada pedido contra un `SelloSeguridad`, bloqueo persistido de 5 intentos / 5 minutos y antiforgery en todo POST/PUT. La configuración del negocio es otra fila única, con decimales persistidos como enteros escalados (ADR-001). El servidor escucha solo en `127.0.0.1` (decisión del 2026-10-16) y el blanqueo de la contraseña es un argumento de línea de comandos que no levanta Kestrel.

## Términos
Se usan los del PRD: **contraseña de acceso**, **sesión**, **bloqueo de ingreso**, **blanqueo**, **Configuración**, **alícuota de IVA**, **condición fiscal** (Responsable Inscripto / Monotributo), **tope de identificación del receptor**, **múltiplo de redondeo comercial**. En código: `Contrasena`, `Sesion`, `BloqueoIngreso`, `Blanqueo`, `ConfiguracionNegocio`, `AlicuotaIva`, `CondicionFiscal` (`ResponsableInscripto`, `Monotributo`), `TopeIdentificacion`, `MultiploRedondeo`.

## Coverage: PRD → blocks
| Requisito | Cubierto por |
|---|---|
| FR-01 | Block 4 (FallbackPolicy, lista blanca de anónimos), Block 5 (redirección de la UI ante 401) |
| FR-02 | Block 3 (`ObtenerEstadoAcceso`), Block 4 (`GET /api/acceso/ingreso`), Block 5 (guarda de ruteo → `CrearContrasena`) |
| FR-03 | Block 2 (`UPDATE … WHERE Hash IS NULL`), Block 3 (`CrearContrasenaInicial` → `ContrasenaYaDefinida`), Block 4 (409), Block 5 |
| FR-04 | Block 1 (`ContrasenaNueva`), Block 3, Block 4 (400 con campo), Block 5 |
| FR-05 | Block 3 (`CambiarContrasena`), Block 4 (`PUT /api/acceso/contrasena`), Block 5 (sección de Configuración) |
| FR-06 | Block 3 (`BlanquearContrasena`), Block 4 (`ComandoBlanqueo`, `docs/operacion/blanqueo-contrasena.md`) |
| FR-07 | Block 3, Block 4 (`/api/configuracion`), Block 5 (página Configuración) |
| FR-08 | Block 1 (`ConfiguracionNegocio.AlicuotaIva`), Block 2 (columna) |
| FR-09 | Block 1 (`CondicionFiscal`), Block 2 (columna con CHECK) |
| FR-10 | Block 1 (`TopeIdentificacion`), Block 2 (columna) |
| FR-11 | Block 1 (`MultiploRedondeo`), Block 2 (columna) |
| FR-12 | Block 1 (validación), Block 4 (400), Block 5 (mensaje) |
| FR-13 | Block 1 (validación), Block 4 (400), Block 5 (mensaje) |
| FR-14 | Block 1 (validación), Block 4 (400), Block 5 (mensaje) |
| FR-15 | Block 1 (`ConfiguracionNegocio.Inicial()`), Block 2 (fila semilla de la migración) |
| FR-16 | Block 5 (`web/src/marca/`, `Layout.tsx`) |
| FR-17 | Block 1 (`ErrorValidacion` con campo y mensaje), Block 4 (`ProblemDetails.errors`), Block 5 (`CampoConError`) |
| NFR-01 | Estrategia: `HasherPbkdf2` (PBKDF2-HMAC-SHA256, sal aleatoria de 16 bytes, 600.000 iteraciones) en Block 2; la contraseña en texto plano nunca se persiste ni se loguea (Block 4). Verificado por tests de inspección de archivos y de logs. |
| NFR-02 | Estrategia: regla en la entidad `CredencialAcceso` (Block 1), persistida (Block 2), con el caso de uso de ingreso serializado por `SemaphoreSlim` y transacción (Block 3); tiempo de `TimeProvider`, verificable con `FakeTimeProvider` (Block 4). |
| NFR-03 | Estrategia: cookie con `ExpireTimeSpan = 60 min` y renovación en cada pedido autenticado (`OnValidatePrincipal` con `ShouldRenew = true`), reloj `TimeProvider` (Block 4); la UI redirige ante 401 (Block 5). |
| NFR-04 | Estrategia: suite Playwright con proyectos `chrome` y `msedge` sobre los flujos del PRD (Block 6); se corre con la versión estable vigente y la anterior en la PC de prueba. |

| AC | Tests (bloque) |
|---|---|
| AC-01 | B4 `Endpoints_SinSesion_Devuelven401_SalvoListaBlanca`; B5 `cliente_ante401_redirigeAIngreso`; B6 `sin sesión redirige a ingreso` |
| AC-02 | B3 `ObtenerEstado_SinContrasena_InformaNoDefinida`; B4 `Estado_SinContrasena_ContrasenaDefinidaFalse`; B5 `App_sinContrasena_muestraCrearContrasena` |
| AC-03 | B2 `CrearCondicional_ConHashExistente_NoModificaFilas`; B3 `CrearInicial_YaDefinida_LanzaContrasenaYaDefinida`; B4 `ContrasenaInicial_YaDefinida_Devuelve409YNoCambiaLaContrasena`, `ContrasenaInicial_Concurrente_SoloUnaGana` |
| AC-04 | B1 `ContrasenaNueva_7Caracteres_Rechaza`; B4 `ContrasenaInicial_7Caracteres_Devuelve400ConCampo`, `CambiarContrasena_Nueva7Caracteres_Devuelve400ConCampo`; B5 `CrearContrasena_muestraMensajeJuntoAlCampo` |
| AC-05 | B3 `Cambiar_ConActualCorrecta_AceptaSoloLaNueva`; B4 `CambiarContrasena_IngresoSiguienteSoloConLaNueva`; B6 `cambio de contraseña` |
| AC-06 | B3 `Cambiar_ConActualIncorrecta_LanzaYMantieneLaAnterior`; B4 `CambiarContrasena_ActualIncorrecta_Devuelve400ConCampo`; B5 `Configuracion_actualIncorrecta_muestraMensaje` |
| AC-07 | B3 `Blanquear_DejaSinContrasenaYRegeneraSello`; B4 `ComandoBlanqueo_DejaSinContrasena_EstadoInformaNoDefinida` |
| AC-08 | B4 `Endpoints_NingunoPermiteBlanquear`, `Arranque_SinFlag_NuncaBlanquea`, `Blanqueo_NoSeDisparaPorConfiguracion` |
| AC-09 | B1 `Credencial_QuintoFallo_Bloquea5Minutos`, `Credencial_Bloqueada_RechazaSinVerificar`; B3 `Ingreso_Bloqueado_NoVerificaHash`; B4 `Ingreso_5Fallos_Bloquea_LiberaA5Minutos`, `Ingreso_FallosConcurrentes_NoSuperan5` |
| AC-10 | B4 `Sesion_60MinutosInactiva_Devuelve401`, `Sesion_ActividadRenuevaElVencimiento`; B6 `vencimiento redirige a ingreso` |
| AC-11 | B2 `HasherPbkdf2_FormatoYVerificacion`, `Base_NoContieneLaContrasenaEnTextoPlano`; B4 `Logs_NoContienenLaContrasena`, `Appsettings_NoContienenLaContrasena` |
| AC-12 | B2 `RepositorioConfiguracion_GuardaYLee`; B3 `ActualizarConfiguracion_Guarda4Valores`; B4 `Configuracion_PutYGet_Devuelve4Valores`; B5 `Configuracion_grabaYMuestraValores`; B6 `configuración` |
| AC-13 | B1 `Configuracion_Multiplo0001_Rechaza`; B4 `Configuracion_Multiplo0001_Devuelve400`; B5 `Configuracion_multiploInvalido_muestraMensaje` |
| AC-14 | B1 `Configuracion_AlicuotaFueraDeRango_Rechaza` (-1, 101); B4 `Configuracion_Alicuota101_Devuelve400`; B5 `Configuracion_alicuotaInvalida_muestraMensaje` |
| AC-15 | B1 `Configuracion_TopeNegativo_Rechaza`; B4 `Configuracion_TopeNegativo_Devuelve400`; B5 `Configuracion_topeNegativo_muestraMensaje` |
| AC-16 | B1 `Configuracion_Inicial_TieneValoresPorDefecto`; B2 `Migracion_SiembraConfiguracionInicial`; B4 `Configuracion_BaseNueva_DevuelveValoresIniciales` |
| AC-17 | B5 `Layout_muestraLogoYColores`, `Ingreso_muestraLogo`; B6 `marca visible` |
| AC-18 | B6 suite completa en proyectos `chrome` y `msedge` |

## Dependencies between blocks
- Block 1 → ninguna (incluye el andamiaje de la solución).
- Block 2 → Block 1.
- Block 3 → Block 1 (usa contratos de Domain; los tests usan dobles, no Block 2).
- Block 4 → Blocks 2 y 3.
- Block 5 → Block 4 (contrato HTTP).
- Block 6 → Blocks 4 y 5.

Orden de ejecución: 1 → 2 → 3 → 4 → 5 → 6 (2 y 3 pueden hacerse en cualquier orden entre sí).

---

## Block 1 — Andamiaje de la solución y dominio

**Files**
- `Optica.sln` (new) — solución con los 4 proyectos de `src/` y los 4 de `tests/`.
- `global.json` (new) — SDK .NET 10.
- `Directory.Build.props` (new) — `net10.0`, `Nullable=enable`, `TreatWarningsAsErrors=true`, `RestorePackagesWithLockFile=true`.
- `.gitignore` (modified) — agrega `bin/`, `obj/`, `node_modules/`, `web/dist/`, `src/Optica.Api/wwwroot/`, `*.db`, `*.db-wal`, `*.db-shm`, `TestResults/`, `playwright-report/`, `test-results/`, `.daw/scripts/__pycache__/`.
- `src/Optica.Domain/Optica.Domain.csproj` (new) — sin referencias a paquetes ni proyectos.
- `src/Optica.Domain/Comun/ErrorValidacion.cs` (new) — excepción de dominio con `Campo` y `Mensaje` (FR-17).
- `src/Optica.Domain/Acceso/ContrasenaNueva.cs` (new) — value object: 8 a 128 caracteres.
- `src/Optica.Domain/Acceso/CredencialAcceso.cs` (new) — entidad de fila única con hash, sello y bloqueo.
- `src/Optica.Domain/Acceso/IHasherContrasena.cs` (new) — `string Hashear(ContrasenaNueva)`, `bool Verificar(string contrasena, string hash)`.
- `src/Optica.Domain/Acceso/IRepositorioCredencial.cs` (new) — `Obtener()`, `CrearSiNoExiste(string hash, Guid sello)` → `bool`, `Guardar(CredencialAcceso)`.
- `src/Optica.Domain/Configuracion/CondicionFiscal.cs` (new) — enum `ResponsableInscripto`, `Monotributo`.
- `src/Optica.Domain/Configuracion/ConfiguracionNegocio.cs` (new) — entidad de fila única con validaciones y `Inicial()`.
- `src/Optica.Domain/Configuracion/IRepositorioConfiguracion.cs` (new) — `Obtener()`, `Guardar(ConfiguracionNegocio)`.
- `tests/Optica.Domain.Tests/Optica.Domain.Tests.csproj` (new) — xUnit, referencia a Domain.
- `tests/Optica.Domain.Tests/ContrasenaNuevaTests.cs` (new)
- `tests/Optica.Domain.Tests/CredencialAccesoTests.cs` (new)
- `tests/Optica.Domain.Tests/ConfiguracionNegocioTests.cs` (new)

**Logic**
- `ContrasenaNueva.Crear(string? valor)`: `null`/vacía o < 8 caracteres → `ErrorValidacion("contrasenaNueva", "La contraseña debe tener al menos 8 caracteres")`; > 128 → `ErrorValidacion("contrasenaNueva", "La contraseña puede tener como máximo 128 caracteres")`. Los espacios cuentan como caracteres; no se recorta.
- `CredencialAcceso` (Id = 1): `Hash` (`string?`), `SelloSeguridad` (`Guid`), `IntentosFallidos` (`int`), `BloqueadoHasta` (`DateTimeOffset?`).
  - `ContrasenaDefinida => Hash is not null`.
  - `EstaBloqueada(ahora) => BloqueadoHasta > ahora`.
  - `RegistrarFallo(ahora)`: `IntentosFallidos++`; si llega a 5 → `BloqueadoHasta = ahora + 5 min`, `IntentosFallidos = 0`.
  - `RegistrarIngresoExitoso()`: `IntentosFallidos = 0`, `BloqueadoHasta = null`.
  - `CambiarHash(nuevoHash)`: asigna hash y regenera `SelloSeguridad`.
  - `Blanquear()`: `Hash = null`, `IntentosFallidos = 0`, `BloqueadoHasta = null`, regenera `SelloSeguridad`.
- `ConfiguracionNegocio` (Id = 1): `AlicuotaIva`, `CondicionFiscal`, `TopeIdentificacion` (`decimal?`), `MultiploRedondeo`. `Inicial()` = 21 / ResponsableInscripto / null / 0,01 (FR-15). `Actualizar(alicuota, condicion, tope, multiplo)` valida todo antes de asignar y acumula los errores por campo (lanza `ErroresValidacion` con la lista).

**Input validation**
| Campo | Tipo | Regla | Mensaje |
|---|---|---|---|
| `contrasenaNueva` / `contrasena` (creación) | string | 8–128 caracteres | "La contraseña debe tener al menos 8 caracteres" / "La contraseña puede tener como máximo 128 caracteres" |
| `alicuotaIva` | decimal | 0 ≤ x ≤ 100, máx. 2 decimales | "La alícuota de IVA debe estar entre 0 y 100" / "Ingresá como máximo 2 decimales" |
| `condicionFiscal` | enum | `ResponsableInscripto` o `Monotributo` | "Elegí una condición fiscal válida" |
| `topeIdentificacion` | decimal? | null o 0 ≤ x ≤ 1.000.000.000, máx. 2 decimales | "El tope no puede ser negativo" / "El tope no puede superar 1.000.000.000" / "Ingresá como máximo 2 decimales" |
| `multiploRedondeo` | decimal | 0,01 ≤ x ≤ 1.000.000, máx. 2 decimales | "El valor mínimo es 0,01" / "El valor máximo es 1.000.000" / "Ingresá como máximo 2 decimales" |

**Error handling**
- E1-1: contraseña corta → `ErrorValidacion` campo `contrasenaNueva`.
- E1-2: contraseña de más de 128 caracteres → `ErrorValidacion`.
- E1-3: alícuota fuera de rango → `ErrorValidacion` campo `alicuotaIva`.
- E1-4: condición fiscal inválida → `ErrorValidacion` campo `condicionFiscal`.
- E1-5: tope negativo o mayor al máximo → `ErrorValidacion` campo `topeIdentificacion`.
- E1-6: múltiplo < 0,01 o > 1.000.000 → `ErrorValidacion` campo `multiploRedondeo`.
- E1-7: más de 2 decimales en un campo decimal → `ErrorValidacion` del campo.

**Required tests**
- [ ] `ContrasenaNueva_7Caracteres_Rechaza` — E1-1, AC-04
- [ ] `ContrasenaNueva_8Caracteres_Acepta` — AC-04 (borde)
- [ ] `ContrasenaNueva_129Caracteres_Rechaza` — E1-2
- [ ] `Credencial_CuatroFallos_NoBloquea` — AC-09 (borde)
- [ ] `Credencial_QuintoFallo_Bloquea5Minutos` — AC-09
- [ ] `Credencial_Bloqueada_RechazaSinVerificar` (`EstaBloqueada` a 4:59 → true, a 5:00 → false) — AC-09
- [ ] `Credencial_IngresoExitoso_ReiniciaContador` — NFR-02
- [ ] `Credencial_CambiarHashYBlanquear_RegeneranSello` — AC-05, AC-07
- [ ] `Configuracion_Inicial_TieneValoresPorDefecto` — AC-16
- [ ] `Configuracion_Multiplo0001_Rechaza` — E1-6, AC-13
- [ ] `Configuracion_MultiploMayorAlMaximo_Rechaza` — E1-6
- [ ] `Configuracion_AlicuotaFueraDeRango_Rechaza` (-1 y 101) — E1-3, AC-14
- [ ] `Configuracion_Alicuota105_Acepta` — AC-12
- [ ] `Configuracion_CondicionInvalida_Rechaza` — E1-4
- [ ] `Configuracion_TopeNegativo_Rechaza` — E1-5, AC-15
- [ ] `Configuracion_TopeMayorAlMaximo_Rechaza` — E1-5
- [ ] `Configuracion_TresDecimales_Rechaza` — E1-7
- [ ] `Configuracion_VariosErrores_InformaTodosLosCampos` — FR-17

**Completion criterion**
`dotnet build Optica.sln` sin warnings, `dotnet test tests/Optica.Domain.Tests` pasa los 18 tests, y `Optica.Domain.csproj` no tiene `PackageReference` ni `ProjectReference`.

---

## Block 2 — Persistencia SQLite e infraestructura

**Files**
- `src/Optica.Infrastructure/Optica.Infrastructure.csproj` (new) — referencia a Domain y Application; paquetes `Microsoft.EntityFrameworkCore.Sqlite` y `Microsoft.EntityFrameworkCore.Design` 10.x.
- `src/Optica.Infrastructure/Persistencia/OpticaDbContext.cs` (new) — mapeo de `CredencialAcceso` y `ConfiguracionNegocio`; convención de decimales.
- `src/Optica.Infrastructure/Persistencia/ConversorDecimalEscalado.cs` (new) — `ValueConverter<decimal, long>` ×10.000 (ADR-001); lanza si hay más de 4 decimales.
- `src/Optica.Infrastructure/Persistencia/InterceptorConexionSqlite.cs` (new) — en cada apertura: `PRAGMA foreign_keys = ON; PRAGMA busy_timeout = 5000;`.
- `src/Optica.Infrastructure/Persistencia/FabricaDisenoDbContext.cs` (new) — `IDesignTimeDbContextFactory` para `dotnet ef`.
- `src/Optica.Infrastructure/Persistencia/Migrations/*_AccesoYConfiguracion.cs` (new) — tablas, CHECK, filas semilla, `PRAGMA journal_mode = WAL`.
- `src/Optica.Infrastructure/Persistencia/RepositorioCredencial.cs` (new)
- `src/Optica.Infrastructure/Persistencia/RepositorioConfiguracion.cs` (new)
- `src/Optica.Infrastructure/Persistencia/UnidadDeTrabajo.cs` (new) — implementa `IUnidadDeTrabajo` (Block 3) con `BeginTransaction`/`Commit`.
- `src/Optica.Infrastructure/Seguridad/HasherPbkdf2.cs` (new)
- `src/Optica.Infrastructure/RegistroInfraestructura.cs` (new) — `AddInfraestructura(IServiceCollection, string rutaBase)`.
- `tests/Optica.Infrastructure.Tests/Optica.Infrastructure.Tests.csproj` (new)
- `tests/Optica.Infrastructure.Tests/BaseTemporal.cs` (new) — crea un archivo SQLite en un directorio temporal único y lo borra al terminar.
- `tests/Optica.Infrastructure.Tests/RepositoriosTests.cs` (new)
- `tests/Optica.Infrastructure.Tests/HasherPbkdf2Tests.cs` (new)
- `tests/Optica.Infrastructure.Tests/ConversorDecimalEscaladoTests.cs` (new)

**Data model**
Tabla `Credencial`:
| Columna | Tipo | Restricciones |
|---|---|---|
| `Id` | INTEGER | PK, `CHECK (Id = 1)` |
| `Hash` | TEXT | NULL; formato `PBKDF2-SHA256$600000$<sal base64>$<hash base64>` |
| `SelloSeguridad` | TEXT | NOT NULL (GUID) |
| `IntentosFallidos` | INTEGER | NOT NULL, DEFAULT 0, `CHECK (IntentosFallidos BETWEEN 0 AND 4)` |
| `BloqueadoHasta` | TEXT | NULL (ISO 8601 UTC) |

Semilla: `(1, NULL, <GUID nuevo>, 0, NULL)`.

Tabla `ConfiguracionNegocio`:
| Columna | Tipo | Restricciones |
|---|---|---|
| `Id` | INTEGER | PK, `CHECK (Id = 1)` |
| `AlicuotaIva` | INTEGER | NOT NULL, ×10.000, `CHECK (AlicuotaIva BETWEEN 0 AND 1000000)` |
| `CondicionFiscal` | TEXT | NOT NULL, `CHECK (CondicionFiscal IN ('ResponsableInscripto','Monotributo'))` |
| `TopeIdentificacion` | INTEGER | NULL, ×10.000, `CHECK (TopeIdentificacion IS NULL OR TopeIdentificacion >= 0)` |
| `MultiploRedondeo` | INTEGER | NOT NULL, ×10.000, `CHECK (MultiploRedondeo >= 100)` |

Semilla: `(1, 210000, 'ResponsableInscripto', NULL, 100)` (FR-15). Sin índices adicionales (tablas de una fila).

**Logic**
- `RepositorioCredencial.CrearSiNoExiste(hash, sello)`: `UPDATE Credencial SET Hash = @hash, SelloSeguridad = @sello WHERE Id = 1 AND Hash IS NULL` parametrizado; devuelve `filasAfectadas == 1`.
- `HasherPbkdf2`: `Rfc2898DeriveBytes.Pbkdf2` con SHA-256, sal de 16 bytes de `RandomNumberGenerator`, 600.000 iteraciones, 32 bytes de salida; `Verificar` compara con `CryptographicOperations.FixedTimeEquals` y rechaza formatos desconocidos.
- La migración se aplica en el arranque (Block 4); no se usa `EnsureCreated`.

**Error handling**
- E2-1: hash con formato desconocido → `Verificar` devuelve `false` (no lanza).
- E2-2: decimal con más de 4 decimales al persistir → `InvalidOperationException` (error de programación; el dominio ya redondea a 2).
- E2-3: escritura que viola un CHECK → `DbUpdateException` propagada; la transacción se revierte.
- E2-4: base ocupada por otra conexión → `busy_timeout` 5 s; si se agota, `SqliteException` propagada (se traduce a 503 en Block 4).
- E2-5: `CrearSiNoExiste` con contraseña ya definida → devuelve `false`, sin modificar la fila.

**Required tests**
- [ ] `Migracion_CreaTablasYSiembraFilas` — AC-16
- [ ] `Migracion_SiembraConfiguracionInicial` (21 / RI / null / 0,01) — AC-16
- [ ] `Migracion_ActivaWalYForeignKeys` — convención de SQLite
- [ ] `CrearCondicional_SinHash_DefineLaContrasena` — FR-02
- [ ] `CrearCondicional_ConHashExistente_NoModificaFilas` — E2-5, AC-03
- [ ] `Credencial_SegundaFila_ViolaCheck` — E2-3, F-03 del threat model
- [ ] `Configuracion_ValorFueraDeCheck_LanzaDbUpdateException` — E2-3
- [ ] `RepositorioConfiguracion_GuardaYLee` (10,5 / Monotributo / 100000 / 50) — AC-12
- [ ] `Conversor_IdaYVuelta_SinPerdida` (10,5; 0,01; 100000) con cultura `es-AR` y `en-US` — ADR-001
- [ ] `Conversor_MasDe4Decimales_Lanza` (0,00001) — E2-2
- [ ] `HasherPbkdf2_FormatoYVerificacion` — NFR-01, AC-11
- [ ] `HasherPbkdf2_MismaContrasena_DistintaSal` — NFR-01
- [ ] `HasherPbkdf2_FormatoDesconocido_DevuelveFalse` — E2-1
- [ ] `Base_NoContieneLaContrasenaEnTextoPlano` (busca los bytes UTF-8 en `optica.db`, `-wal` y `-shm`) — AC-11
- [ ] `UnidadDeTrabajo_ExcepcionRevierte` — E2-3
- [ ] `ConexionOcupada_EsperaBusyTimeout` — E2-4

**Completion criterion**
`dotnet test tests/Optica.Infrastructure.Tests` pasa los 16 tests sobre bases temporales que se borran al terminar, y `dotnet ef migrations list` muestra `AccesoYConfiguracion`.

---

## Block 3 — Casos de uso de acceso y configuración

**Files**
- `src/Optica.Application/Optica.Application.csproj` (new) — referencia a Domain.
- `src/Optica.Application/Comun/IUnidadDeTrabajo.cs` (new) — `Task EjecutarAsync(Func<Task>)` con transacción.
- `src/Optica.Application/Comun/Errores.cs` (new) — `ContrasenaYaDefinida`, `ContrasenaNoDefinida`, `IngresoBloqueado(DateTimeOffset hasta)`, `ContrasenaIncorrecta(int intentosRestantes)`, `ContrasenaActualIncorrecta`.
- `src/Optica.Application/Acceso/ObtenerEstadoAcceso.cs` (new)
- `src/Optica.Application/Acceso/CrearContrasenaInicial.cs` (new)
- `src/Optica.Application/Acceso/IniciarSesion.cs` (new) — devuelve el `SelloSeguridad` vigente.
- `src/Optica.Application/Acceso/ValidarSesion.cs` (new) — compara el sello del claim con el de la base.
- `src/Optica.Application/Acceso/CambiarContrasena.cs` (new)
- `src/Optica.Application/Acceso/BlanquearContrasena.cs` (new)
- `src/Optica.Application/Configuracion/ObtenerConfiguracion.cs` (new)
- `src/Optica.Application/Configuracion/ActualizarConfiguracion.cs` (new)
- `src/Optica.Application/RegistroAplicacion.cs` (new) — `AddAplicacion(IServiceCollection)`.
- `tests/Optica.Application.Tests/Optica.Application.Tests.csproj` (new) — incluye `Microsoft.Extensions.TimeProvider.Testing`.
- `tests/Optica.Application.Tests/Dobles/RepositorioCredencialEnMemoria.cs` (new)
- `tests/Optica.Application.Tests/Dobles/RepositorioConfiguracionEnMemoria.cs` (new)
- `tests/Optica.Application.Tests/Dobles/HasherFalso.cs` (new) — registra si se llamó a `Verificar`.
- `tests/Optica.Application.Tests/Dobles/UnidadDeTrabajoInmediata.cs` (new)
- `tests/Optica.Application.Tests/AccesoTests.cs` (new)
- `tests/Optica.Application.Tests/ConfiguracionTests.cs` (new)

**Logic**
- Tiempo: todos los casos de uso reciben `TimeProvider` (BCL); no hay un reloj propio.
- `ObtenerEstadoAcceso` → `{ ContrasenaDefinida, BloqueadoHasta }` (`BloqueadoHasta` solo si sigue vigente).
- `CrearContrasenaInicial(string contrasena)`: valida `ContrasenaNueva`; `CrearSiNoExiste(hash, Guid.NewGuid())`; si devuelve `false` → `ContrasenaYaDefinida`.
- `IniciarSesion(string contrasena)`: serializado con un `SemaphoreSlim(1)` estático y ejecutado en `IUnidadDeTrabajo`:
  1. Sin contraseña definida → `ContrasenaNoDefinida`.
  2. Bloqueada → `IngresoBloqueado(hasta)` **sin** llamar a `Verificar` y sin extender el bloqueo.
  3. Hash incorrecto → `RegistrarFallo`, guardar, `IngresoBloqueado` si se acaba de bloquear o, si no, `ContrasenaIncorrecta(5 - intentos)`.
  4. Correcta → `RegistrarIngresoExitoso`, guardar, devolver el sello.
- `ValidarSesion(Guid sello)` → `true` solo si hay contraseña definida y el sello coincide.
- `CambiarContrasena(actual, nueva)`: verifica la actual (los fallos **no** cuentan para el bloqueo: requiere sesión); valida `ContrasenaNueva`; `CambiarHash`; devuelve el sello nuevo. Errores de las dos validaciones se informan por campo.
- `BlanquearContrasena()`: `Blanquear()` y guardar; devuelve si había contraseña definida.
- `ObtenerConfiguracion` / `ActualizarConfiguracion(dto)`: `ConfiguracionNegocio.Actualizar(...)` en `IUnidadDeTrabajo`.
- Ningún caso de uso loguea contraseñas ni hashes.

**Error handling**
- E3-1: `ContrasenaYaDefinida` (creación con contraseña existente).
- E3-2: `ContrasenaNoDefinida` (ingreso sin contraseña creada).
- E3-3: `IngresoBloqueado(hasta)`.
- E3-4: `ContrasenaIncorrecta(intentosRestantes)`.
- E3-5: `ContrasenaActualIncorrecta` (campo `contrasenaActual`).
- E3-6: `ErrorValidacion` / `ErroresValidacion` del dominio, propagados sin cambios.
- E3-7: falla de persistencia dentro de la unidad de trabajo → se revierte y se propaga.

**Required tests**
- [ ] `ObtenerEstado_SinContrasena_InformaNoDefinida` — AC-02
- [ ] `ObtenerEstado_BloqueoVencido_NoInformaBloqueo` — NFR-02
- [ ] `CrearInicial_Valida_DefineLaContrasena` — FR-02
- [ ] `CrearInicial_YaDefinida_LanzaContrasenaYaDefinida` — E3-1, AC-03
- [ ] `CrearInicial_Corta_LanzaErrorValidacion` — E3-6, AC-04
- [ ] `Ingreso_SinContrasena_LanzaContrasenaNoDefinida` — E3-2
- [ ] `Ingreso_Correcta_DevuelveSelloYReiniciaContador` — FR-01
- [ ] `Ingreso_Incorrecta_InformaIntentosRestantes` — E3-4
- [ ] `Ingreso_QuintoFallo_LanzaIngresoBloqueado` — E3-3, AC-09
- [ ] `Ingreso_Bloqueado_NoVerificaHash` — E3-3, AC-09
- [ ] `Ingreso_BloqueoVencido_AceptaCorrecta` (FakeTimeProvider +5 min) — AC-09
- [ ] `ValidarSesion_SelloAnterior_DevuelveFalse` — F-01 del threat model
- [ ] `Cambiar_ConActualCorrecta_AceptaSoloLaNueva` — AC-05
- [ ] `Cambiar_ConActualIncorrecta_LanzaYMantieneLaAnterior` — E3-5, AC-06
- [ ] `Cambiar_FallosNoCuentanParaElBloqueo` — NFR-02
- [ ] `Cambiar_NuevaCorta_LanzaErrorValidacion` — E3-6, AC-04
- [ ] `Blanquear_DejaSinContrasenaYRegeneraSello` — AC-07
- [ ] `Blanquear_SinContrasena_EsIdempotente` — FR-06
- [ ] `ActualizarConfiguracion_Guarda4Valores` — AC-12
- [ ] `ActualizarConfiguracion_Invalida_NoGuardaNada` — E3-6, AC-13, AC-14, AC-15
- [ ] `UnidadDeTrabajo_FallaPersistencia_Propaga` — E3-7

**Completion criterion**
`dotnet test tests/Optica.Application.Tests` pasa los 21 tests sin tocar SQLite, y `Optica.Application.csproj` solo referencia a Domain.

---

## Block 4 — API, sesión, seguridad, blanqueo y documentación de operación

**Files**
- `src/Optica.Api/Optica.Api.csproj` (new) — referencia a Application e Infrastructure; paquete `Microsoft.Extensions.Hosting.WindowsServices`.
- `src/Optica.Api/Program.cs` (new) — composition root; detección del blanqueo en `args` crudos antes de crear el builder.
- `src/Optica.Api/Configuracion/OpcionesOptica.cs` (new) — `RutaBase`, `DirectorioClaves`, `DirectorioAuditoria`; valida que ninguna ruta caiga dentro del árbol de fuentes.
- `src/Optica.Api/Seguridad/ConfiguracionSesion.cs` (new) — cookie, `OnValidatePrincipal`, `OnRedirectToLogin`/`OnRedirectToAccessDenied`.
- `src/Optica.Api/Seguridad/EncabezadosSeguridad.cs` (new) — middleware de headers.
- `src/Optica.Api/Controllers/AccesoController.cs` (new)
- `src/Optica.Api/Controllers/ConfiguracionController.cs` (new)
- `src/Optica.Api/Errores/ManejadorErrores.cs` (new) — `IExceptionHandler` → `ProblemDetails`.
- `src/Optica.Api/Herramientas/ComandoBlanqueo.cs` (new)
- `src/Optica.Api/appsettings.json` (new) — `Urls: http://127.0.0.1:5080`, `AllowedHosts: localhost;127.0.0.1`, rutas por defecto en `%ProgramData%\OpticaSistema\`, logging sin cuerpos.
- `src/Optica.Api/appsettings.Development.json` (new) — rutas en un directorio de datos del usuario fuera del repo.
- `src/Optica.Api/Properties/launchSettings.json` (new) — perfil `http` en `127.0.0.1:5080`.
- `docs/operacion/instalacion.md` (new) — instalación como servicio, ACL de `%ProgramData%\OpticaSistema\`, crear la contraseña al instalar, acceso directo a `http://localhost:5080`.
- `docs/operacion/blanqueo-contrasena.md` (new) — procedimiento de blanqueo.
- `tests/Optica.Api.Tests/Optica.Api.Tests.csproj` (new) — `Microsoft.AspNetCore.Mvc.Testing`, `Microsoft.Extensions.TimeProvider.Testing`.
- `tests/Optica.Api.Tests/FabricaApi.cs` (new) — `WebApplicationFactory` con base temporal, `FakeTimeProvider` y captura de logs.
- `tests/Optica.Api.Tests/AccesoApiTests.cs` (new)
- `tests/Optica.Api.Tests/ConfiguracionApiTests.cs` (new)
- `tests/Optica.Api.Tests/SeguridadApiTests.cs` (new)
- `tests/Optica.Api.Tests/ComandoBlanqueoTests.cs` (new)

**Logic**
- **Arranque:** si `args` contiene exactamente `--blanquear-contrasena` → `ComandoBlanqueo.Ejecutar(args)` (host mínimo `Host.CreateApplicationBuilder` sin Kestrel, registra Infraestructura y Aplicación, aplica migraciones, ejecuta `BlanquearContrasena`, escribe en `{DirectorioAuditoria}/auditoria-blanqueo.log` una línea `fecha-hora ISO UTC | usuario de Windows | resultado`, devuelve el código de salida). El flag nunca se lee de `IConfiguration` ni de variables de entorno.
- **Servidor:** `UseWindowsService()`; `TimeProvider.System` como singleton; valida `OpcionesOptica` (falla si alguna ruta está dentro de un directorio que contiene `Optica.sln` o `.git`); crea los directorios; `Database.Migrate()`; Data Protection con `PersistKeysToFileSystem(DirectorioClaves)` y `ProtectKeysWithDpapi()` en Windows; `UseHostFiltering`; `EncabezadosSeguridad`; autenticación por cookie; `FallbackPolicy` = usuario autenticado; antiforgery con header `X-XSRF-TOKEN` y cookie `XSRF-TOKEN` legible por JS; `UseStaticFiles` + `MapFallbackToFile("index.html").AllowAnonymous()`.
- **Sesión:** cookie `optica.sesion`, `HttpOnly`, `SameSite=Strict`, `SecurePolicy=None` (solo loopback, AR-01), `ExpireTimeSpan = 60 min`, `SlidingExpiration = false` y renovación manual en cada pedido autenticado (`OnValidatePrincipal` → `ValidarSesion(sello)`; sello inválido → `RejectPrincipal` + `SignOut`; válido → `ShouldRenew = true`). `OnRedirectToLogin`/`OnRedirectToAccessDenied` → 401/403 sin redirect.
- **Headers:** `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, `Referrer-Policy: no-referrer`, `Content-Security-Policy: default-src 'self'; img-src 'self' data:; frame-ancestors 'none'`.
- **Logs:** sin `HttpLogging`; los controladores loguean eventos (`ContrasenaCreada`, `IngresoExitoso`, `IngresoFallido`, `IngresoBloqueado`, `ContrasenaCambiada`, `ConfiguracionActualizada`) con fecha y hora, sin contraseñas, hashes ni cookies.
- **Anónimos (lista blanca):** `GET` y `POST /api/acceso/ingreso` (endpoint de ingreso de FR-01), `POST /api/acceso/contrasena-inicial` (creación inicial de FR-01), archivos estáticos y fallback del SPA (la pantalla de ingreso). `GET /api/acceso/ingreso` no devuelve datos de negocio.

**API contract**
Formato de error común: `application/problem+json` (RFC 9457) con `title`, `status` y, en 400 de validación, `errors: { "<campo>": ["<mensaje>"] }`. Todo POST/PUT exige el header `X-XSRF-TOKEN`; si falta o es inválido → 400 `{ title: "Token antiforgery inválido" }`.

1. `GET /api/acceso/ingreso` — Auth: anónimo.
   - Response 200: `{ contrasenaDefinida: boolean, bloqueadoHasta: string | null, sesionActiva: boolean }` (`bloqueadoHasta` en ISO 8601 UTC; `sesionActiva` indica si la cookie del pedido es una sesión válida). Emite la cookie `XSRF-TOKEN`.
   - Errores: 400 si el `Host` no está permitido (E4-4).
2. `POST /api/acceso/contrasena-inicial` — Auth: anónimo + antiforgery.
   - Request: `{ contrasena: string }` (8–128 caracteres).
   - Response 204 (no inicia sesión; la UI pasa a la pantalla de ingreso).
   - Errores: 400 `errors.contrasena`; 409 `{ title: "La contraseña ya está definida" }`; 400 antiforgery.
3. `POST /api/acceso/ingreso` — Auth: anónimo + antiforgery.
   - Request: `{ contrasena: string }` (máx. 128 caracteres; no se aplica la regla de mínimo).
   - Response 204 + `Set-Cookie: optica.sesion` + nueva cookie `XSRF-TOKEN`.
   - Errores: 401 `{ title: "La contraseña no es correcta", intentosRestantes: number }`; 423 `{ title: "Ingreso bloqueado", bloqueadoHasta: string }`; 409 `{ title: "Todavía no se creó la contraseña" }`; 400 antiforgery o cuerpo inválido.
4. `POST /api/acceso/salida` — Auth: sesión + antiforgery. Response 204 (borra la cookie). Errores: 401.
5. `PUT /api/acceso/contrasena` — Auth: sesión + antiforgery.
   - Request: `{ contrasenaActual: string, contrasenaNueva: string }`.
   - Response 204 + cookie de sesión reemitida con el sello nuevo (las demás sesiones quedan inválidas).
   - Errores: 400 `errors.contrasenaActual` ("La contraseña actual no es correcta") y/o `errors.contrasenaNueva`; 401.
6. `GET /api/configuracion` — Auth: sesión.
   - Response 200: `{ alicuotaIva: number, condicionFiscal: "ResponsableInscripto" | "Monotributo", topeIdentificacion: number | null, multiploRedondeo: number }`.
   - Errores: 401.
7. `PUT /api/configuracion` — Auth: sesión + antiforgery.
   - Request: mismo cuerpo que la respuesta de 6; los 4 campos son obligatorios (`topeIdentificacion` admite `null`).
   - Response 200: configuración guardada.
   - Errores: 400 `errors.<campo>` (mensajes de Block 1); 400 cuerpo inválido o campo faltante; 401.

Mapeo de errores en `ManejadorErrores`: `ErrorValidacion`/`ErroresValidacion` → 400; `ContrasenaActualIncorrecta` → 400 campo `contrasenaActual`; `ContrasenaIncorrecta` → 401; `IngresoBloqueado` → 423; `ContrasenaYaDefinida` y `ContrasenaNoDefinida` → 409; `SqliteException` por base ocupada → 503 `{ title: "La base está ocupada, intentá de nuevo" }`; cualquier otra → 500 sin detalle fuera de Development.

**Input validation**
- Cuerpos JSON: `System.Text.Json` estricto (`UnmappedMemberHandling = Disallow`); campo faltante o tipo erróneo → 400 con el campo.
- Tamaño máximo del cuerpo en `/api/acceso/*` y `/api/configuracion`: 4 KB.
- `contrasena`/`contrasenaActual`/`contrasenaNueva`: string, máx. 128; reglas de dominio de Block 1.
- Configuración: reglas de Block 1; `condicionFiscal` acepta solo los dos nombres exactos.
- Header `Host`: solo `localhost` y `127.0.0.1` (con puerto).

**Error handling**
- E4-1: pedido sin sesión a un endpoint protegido → 401 (sin redirect).
- E4-2: cookie con sello anterior → 401 y la cookie se borra.
- E4-3: falta o es inválido el token antiforgery → 400.
- E4-4: `Host` no permitido → 400.
- E4-5: validación → 400 con `errors` por campo.
- E4-6: contraseña ya definida / no definida → 409.
- E4-7: contraseña incorrecta → 401 con `intentosRestantes`.
- E4-8: bloqueado → 423 con `bloqueadoHasta`.
- E4-9: base ocupada → 503.
- E4-10: error inesperado → 500 sin stack trace.
- E4-11: `RutaBase`, `DirectorioClaves` o `DirectorioAuditoria` dentro del repo → el proceso no arranca (código ≠ 0, mensaje claro).
- E4-12: comando de blanqueo sin permisos de escritura o con la base inaccesible → código de salida 1, mensaje en consola y línea de auditoría con el error.
- E4-13: cuerpo mayor a 4 KB → 413.

**Required tests**
- [ ] `Endpoints_SinSesion_Devuelven401_SalvoListaBlanca` (recorre `EndpointDataSource`) — E4-1, AC-01
- [ ] `Endpoints_NingunoPermiteBlanquear` (ninguna ruta contiene "blanque" ni toca la credencial salvo las 3 de acceso documentadas) — AC-08
- [ ] `Estado_SinContrasena_ContrasenaDefinidaFalse` — AC-02
- [ ] `ContrasenaInicial_Valida_Devuelve204` — FR-02
- [ ] `ContrasenaInicial_7Caracteres_Devuelve400ConCampo` — E4-5, AC-04
- [ ] `ContrasenaInicial_YaDefinida_Devuelve409YNoCambiaLaContrasena` — E4-6, AC-03
- [ ] `ContrasenaInicial_Concurrente_SoloUnaGana` (2 pedidos en paralelo) — AC-03
- [ ] `Ingreso_SinContrasenaCreada_Devuelve409` — E4-6
- [ ] `Ingreso_Incorrecta_Devuelve401ConIntentosRestantes` — E4-7
- [ ] `Ingreso_5Fallos_Bloquea_LiberaA5Minutos` (FakeTimeProvider: 4:59 → 423 con la correcta; 5:00 → 204) — E4-8, AC-09
- [ ] `Ingreso_FallosConcurrentes_NoSuperan5` (10 pedidos en paralelo → como máximo 5 verificaciones) — AC-09
- [ ] `Sesion_60MinutosInactiva_Devuelve401` — AC-10
- [ ] `Sesion_ActividadRenuevaElVencimiento` (pedido a los 50 min, otro a los 100 min → 200) — NFR-03
- [ ] `CambiarContrasena_IngresoSiguienteSoloConLaNueva` — AC-05
- [ ] `CambiarContrasena_OtraSesionQuedaInvalida` — E4-2
- [ ] `CambiarContrasena_ActualIncorrecta_Devuelve400ConCampo` — E4-5, AC-06
- [ ] `CambiarContrasena_Nueva7Caracteres_Devuelve400ConCampo` — E4-5, AC-04
- [ ] `Configuracion_BaseNueva_DevuelveValoresIniciales` — AC-16
- [ ] `Configuracion_PutYGet_Devuelve4Valores` — AC-12
- [ ] `Configuracion_Multiplo0001_Devuelve400` — E4-5, AC-13
- [ ] `Configuracion_Alicuota101_Devuelve400` — E4-5, AC-14
- [ ] `Configuracion_TopeNegativo_Devuelve400` — E4-5, AC-15
- [ ] `Configuracion_CampoFaltante_Devuelve400` — E4-5
- [ ] `Post_SinTokenAntiforgery_Devuelve400` (ingreso, contraseña-inicial, configuración) — E4-3
- [ ] `HostNoPermitido_Devuelve400` (`Host: atacante.example`) — E4-4
- [ ] `CuerpoMayorA4KB_Devuelve413` — E4-13
- [ ] `BaseOcupada_Devuelve503` — E4-9
- [ ] `ErrorInesperado_Devuelve500SinStackTrace` — E4-10
- [ ] `Respuestas_IncluyenEncabezadosDeSeguridad` — threat model R-11
- [ ] `Logs_NoContienenLaContrasena` (busca la contraseña en todos los logs capturados) — AC-11
- [ ] `Appsettings_NoContienenLaContrasena` — AC-11
- [ ] `Arranque_RutaBaseDentroDelRepo_NoArranca` — E4-11
- [ ] `ComandoBlanqueo_DejaSinContrasena_EstadoInformaNoDefinida` — AC-07
- [ ] `ComandoBlanqueo_InvalidaSesionesAbiertas` — E4-2, AC-07
- [ ] `ComandoBlanqueo_EscribeAuditoria` — threat model C5
- [ ] `ComandoBlanqueo_BaseInaccesible_SaleConCodigo1` — E4-12
- [ ] `Arranque_SinFlag_NuncaBlanquea` — AC-08
- [ ] `Blanqueo_NoSeDisparaPorConfiguracion` (variable de entorno y appsettings con el nombre del flag) — AC-08

**Completion criterion**
`dotnet test tests/Optica.Api.Tests` pasa los 38 tests; `dotnet run --project src/Optica.Api` escucha solo en `127.0.0.1:5080` (`netstat` no muestra `0.0.0.0:5080`); `curl -H "Host: localhost:5080" http://127.0.0.1:5080/api/configuracion` devuelve 401.

---

## Block 5 — Interfaz web: marca, ingreso, creación de contraseña y Configuración

**Files**
- `web/package.json` (new) — React 19, react-router-dom, Vite, TypeScript, Vitest, Testing Library; versiones exactas y `package-lock.json`.
- `web/package-lock.json` (new)
- `web/tsconfig.json`, `web/tsconfig.node.json` (new)
- `web/vite.config.ts` (new) — proxy `/api` → `http://127.0.0.1:5080`; alias `@marca` → `../Marca`; `server.fs.allow: ['..']`; `build.outDir: dist`.
- `web/vitest.setup.ts` (new)
- `web/index.html` (new)
- `web/src/main.tsx` (new)
- `web/src/App.tsx` (new) — rutas y guarda: consulta `/api/acceso/ingreso`; sin contraseña → `/crear-contrasena`; sin sesión → `/ingreso`; con sesión → `/configuracion`.
- `web/src/marca/marca.ts` (new) — importa `@marca/branding.json` y `@marca/logo.png`; expone colores y logo.
- `web/src/marca/marca.css` (new) — variables `--color-primario` y `--color-fondo`.
- `web/src/componentes/Layout.tsx` (new) — encabezado con logo, paleta y botón Salir.
- `web/src/componentes/CampoConError.tsx` (new) — input con mensaje de error junto al campo.
- `web/src/api/cliente.ts` (new) — `fetch` con `credentials: 'same-origin'`, header `X-XSRF-TOKEN` leído de la cookie, traducción de `ProblemDetails.errors` a errores por campo, 401 → evento de redirección a ingreso.
- `web/src/paginas/Ingreso.tsx` (new)
- `web/src/paginas/CrearContrasena.tsx` (new)
- `web/src/paginas/Configuracion.tsx` (new) — parámetros del negocio + cambio de contraseña.
- `web/src/**/*.test.tsx` (new) — tests de los componentes y páginas listados abajo.
- `src/Optica.Api/Optica.Api.csproj` (modified) — target `PublicarWeb` en `Publish` que ejecuta `npm ci && npm run build` en `web/` y copia `web/dist` a `wwwroot`.

**Logic**
- Todas las pantallas (incluidas Ingreso y CrearContrasena) usan `Layout` con el logo y `#0903A0` sobre `#FFFFFF` tomados de `branding.json`, no escritos a mano en el código (FR-16).
- `CrearContrasena`: campo contraseña + confirmación; si no coinciden → "Las contraseñas no coinciden" (validación solo de UI); envía a `contrasena-inicial`; 204 → `/ingreso`; 409 → `/ingreso` con aviso "La contraseña ya estaba definida".
- `Ingreso`: 204 → `/configuracion`; 401 → "La contraseña no es correcta. Te quedan N intentos."; 423 → "Ingreso bloqueado hasta HH:MM"; 409 → `/crear-contrasena`.
- `Configuracion`: carga `GET /api/configuracion`; muestra alícuota, condición fiscal (select de 2 opciones), tope (vacío = sin cargar) y múltiplo; Grabar → `PUT`; muestra los errores junto a cada campo; éxito → "Configuración guardada". Sección "Cambiar contraseña" con actual, nueva y confirmación.
- Números: la UI acepta coma decimal (formato es-AR) y envía `number` en el JSON.
- La contraseña solo vive en el estado del formulario y se limpia tras cada envío; nunca se guarda en `localStorage`/`sessionStorage`.
- Ante 401 en cualquier pedido autenticado → redirige a `/ingreso` con "Tu sesión venció. Ingresá de nuevo."

**Input validation**
- Contraseña: `maxLength=128`; los mínimos los valida el servidor (el mensaje se muestra junto al campo).
- Alícuota, tope, múltiplo: `inputmode="decimal"`; texto que no es número → "Ingresá un número" junto al campo (sin enviar).
- Condición fiscal: solo las dos opciones del select.

**Error handling**
- E5-1: 400 con `errors` → mensaje junto a cada campo (FR-17).
- E5-2: 401 en pedido autenticado → redirección a ingreso con aviso.
- E5-3: 401 en ingreso → mensaje con intentos restantes.
- E5-4: 423 → mensaje con la hora de liberación.
- E5-5: 409 en creación inicial → ir a ingreso con aviso; 409 en ingreso → ir a crear contraseña.
- E5-6: 5xx o red caída → "No se pudo conectar con el sistema. Intentá de nuevo." sin perder lo cargado.
- E5-7: confirmación distinta → "Las contraseñas no coinciden" (sin enviar).
- E5-8: valor no numérico → "Ingresá un número" (sin enviar).

**Required tests**
- [ ] `Layout_muestraLogoYColores` — AC-17
- [ ] `Ingreso_muestraLogo` — AC-17
- [ ] `App_sinContrasena_muestraCrearContrasena` — AC-02
- [ ] `cliente_ante401_redirigeAIngreso` — E5-2, AC-01, AC-10
- [ ] `CrearContrasena_muestraMensajeJuntoAlCampo` (400 → "La contraseña debe tener al menos 8 caracteres") — E5-1, AC-04
- [ ] `CrearContrasena_confirmacionDistinta_noEnvia` — E5-7
- [ ] `CrearContrasena_409_vaAIngresoConAviso` — E5-5
- [ ] `Ingreso_401_muestraIntentosRestantes` — E5-3
- [ ] `Ingreso_423_muestraHoraDeLiberacion` — E5-4, AC-09
- [ ] `Ingreso_409_vaACrearContrasena` — E5-5
- [ ] `Configuracion_grabaYMuestraValores` — AC-12
- [ ] `Configuracion_multiploInvalido_muestraMensaje` ("El valor mínimo es 0,01") — E5-1, AC-13
- [ ] `Configuracion_alicuotaInvalida_muestraMensaje` — E5-1, AC-14
- [ ] `Configuracion_topeNegativo_muestraMensaje` — E5-1, AC-15
- [ ] `Configuracion_valorNoNumerico_noEnvia` — E5-8
- [ ] `Configuracion_actualIncorrecta_muestraMensaje` — E5-1, AC-06
- [ ] `Configuracion_errorDeRed_conservaLoCargado` — E5-6
- [ ] `Contrasena_noSeGuardaEnStorage` — threat model C1

**Completion criterion**
`npm run test` en `web/` pasa los 18 tests; `npm run build` genera `web/dist` sin errores de TypeScript; `dotnet publish src/Optica.Api` deja `index.html` en `wwwroot` del publish.

---

## Block 6 — Pruebas de punta a punta en Chrome y Edge

**Files**
- `e2e/package.json` (new) — `@playwright/test` con versión exacta.
- `e2e/package-lock.json` (new)
- `e2e/playwright.config.ts` (new) — proyectos `chrome` (`channel: 'chrome'`) y `msedge` (`channel: 'msedge'`); `webServer` que publica y levanta la API con `ASPNETCORE_ENVIRONMENT=E2E` y una base temporal fuera del repo.
- `src/Optica.Api/appsettings.E2E.json` (new) — solo rutas temporales de base, claves y auditoría; no habilita ningún endpoint adicional.
- `e2e/tests/acceso.spec.ts` (new)
- `e2e/tests/configuracion.spec.ts` (new)
- `docs/operacion/pruebas-e2e.md` (new) — cómo correr la suite en la PC con Chrome y Edge (versión vigente y anterior); qué hacer si falta un navegador (Playwright falla con un mensaje explícito).

**Logic**
Flujos sobre base vacía: crear contraseña → ingresar → ver marca → configurar los 4 parámetros → cambiar contraseña → salir → ingresar con la nueva; acceso sin sesión → redirección. El vencimiento se simula borrando la cookie `optica.sesion` del contexto del navegador y navegando: verifica la redirección de la UI ante 401. El vencimiento real a los 60 minutos se prueba en Block 4 con `FakeTimeProvider` (`Sesion_60MinutosInactiva_Devuelve401`). Este bloque no agrega endpoints ni código de producción.

**Error handling**
- E6-1: la API no responde 401 al perder la sesión → el test `vencimiento redirige a ingreso` falla (es el error que este bloque detecta en la UI).

**Required tests**
- [ ] `creación de contraseña e ingreso` — AC-02, AC-05
- [ ] `sin sesión redirige a ingreso` — AC-01
- [ ] `marca visible en ingreso y configuración` — AC-17
- [ ] `configuración: graba y muestra los 4 valores; múltiplo 0,001 muestra error` — AC-12, AC-13
- [ ] `cambio de contraseña` — AC-05
- [ ] `vencimiento redirige a ingreso` (cookie borrada → pantalla de ingreso con "Tu sesión venció. Ingresá de nuevo.") — E6-1, AC-10
- [ ] Toda la suite en los proyectos `chrome` y `msedge` — AC-18

**Completion criterion**
`npx playwright test` en `e2e/` pasa los 6 tests en los proyectos `chrome` y `msedge`.

---

## Final verification
- `dotnet build Optica.sln` sin warnings y `dotnet test` en verde (Domain 18, Infrastructure 16, Application 21, Api 38).
- `npm run test` en `web/` (18) y `npx playwright test` en `e2e/` (6 × 2 navegadores) en verde.
- Reglas de dependencia: Domain sin referencias; Application → Domain; Infrastructure → Domain y Application; Api → Application e Infrastructure. Nada de EF Core ni SQLite fuera de Infrastructure.
- Todos los AC-01 a AC-18 del PRD tienen al menos un test en verde (tabla de cobertura).
- Las 12 mitigaciones de `docs/daw/security/threat-FEAT-001a.md` §9 están implementadas.
- `git status` no muestra archivos `*.db*` ni directorios de claves.

## Rollback
Primer ticket con código: no hay datos previos ni migraciones anteriores. Revertir = revertir los commits del ticket; en una instalación, detener el servicio y borrar `%ProgramData%\OpticaSistema\` (todavía no contiene datos de clientes).
