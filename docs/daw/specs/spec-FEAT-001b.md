# Spec FEAT-001b: Catálogo de artículos y cálculo del precio de venta

| Field | Value |
|-------|-------|
| Ticket | FEAT-001b |
| PRD | docs/daw/prd/prd-FEAT-001b.md |
| Tier | FEATURE |
| Date | 2026-10-16 |
| Spec loops | 0 |
| Threat model | docs/daw/security/threat-FEAT-001b.md |
| ADR | docs/adr/adr-002-lector-xlsx-openxml.md |

## Summary

Se agrega el módulo Catálogo sobre la arquitectura de FEAT-001a:

- **Dominio:** entidad `Articulo` y servicio puro `CalculadoraPrecioVenta` con aritmética `decimal`. El resultado exacto se redondea a 6 decimales y después se lleva hacia arriba al múltiplo comercial. Además, el servicio `RecalculoPrecios`.
- **Persistencia:** tabla `Articulos` en SQLite, con importes ×10.000 (ADR-001) y CHECK de los topes.
- **Aplicación:** casos de uso del ABM, búsqueda e importación.
- **Configuración:** `ActualizarConfiguracion` recalcula todo el catálogo dentro de su propia transacción.
- **Planilla:** el `.xlsx` se lee en memoria con DocumentFormat.OpenXml en modo SAX, detrás del puerto `ILectorPlanillaCostos` (ADR-002), con defensas contra ZIP bomb y límite de 2 MB.
- **API y web:** controladores sin lógica y pantallas Catálogo (pantalla de inicio después del ingreso) e Importar planilla.
- **Datos confidenciales:** los costos y márgenes de proveedores no se escriben en archivos temporales, logs ni caché del navegador.

## Terms

Se usan los del PRD:

| Término del PRD | Nombre en el código |
|---|---|
| Código | `Codigo` (autonumérico) |
| Código en el proveedor | `CodigoProveedor` |
| Descripción | `Descripcion` |
| Precio de Costo | `PrecioCosto` |
| Margen de Utilidad | `MargenUtilidad` |
| Precio de Venta | `PrecioVenta` |
| activo / inactivo | `Activo` |
| planilla de costos | `PlanillaCostos` |
| múltiplo de redondeo comercial | `MultiploRedondeo` (FEAT-001a) |

Normalizaciones de texto:

- `CodigoProveedorClave`: código en el proveedor sin espacios al principio ni al final y en minúsculas invariantes. Conserva los acentos (FR-13).
- `TextoBusqueda`: texto en minúsculas, sin acentos y sin espacios al borde (FR-22).

## Coverage: PRD → blocks

| Requirement | Covered by |
|---|---|
| FR-01 | Block 1 (`Articulo.Crear`), Block 2 (tabla), Block 3 (`CrearArticulo`), Block 5 (`POST /api/articulos`), Block 6 (formulario) |
| FR-02 | Block 2 (`Codigo` INTEGER PK AUTOINCREMENT), Block 3 |
| FR-03 | Block 1 (`CalculadoraPrecioVenta`), Block 3 |
| FR-04 | Block 1 (`CalculadoraPrecioVenta`, rama Responsable Inscripto) |
| FR-05 | Block 1 (`CalculadoraPrecioVenta`, rama Monotributo) |
| FR-06 | Block 1 (redondeo a 6 decimales y techo al múltiplo) |
| FR-07 | Block 1 (`Articulo.PrecioVenta` ya redondeado), Block 2 (columna) |
| FR-08 | Block 6 (campo de solo lectura), Block 5 (el DTO de entrada no acepta `precioVenta`) |
| FR-09 | Block 1 (`Articulo.Modificar`), Block 3 (`ModificarArticulo`) |
| FR-10 | Block 1 (`RecalculoPrecios`), Block 3 (`ActualizarConfiguracion` modificado), Block 2 (`GuardarLote`) |
| FR-11 | Block 4 (`LectorPlanillaCostosXlsx`), Block 5 (`POST /api/articulos/planilla`), Block 6 (`ImportarPlanilla`) |
| FR-12 | Block 3 (`ImportarCostos`) |
| FR-13 | Block 1 (`CodigoProveedorClave`), Block 2 (columna indexada), Block 3 |
| FR-14 | Block 4 (validación del formato), Block 3 (no actualiza nada) |
| FR-15 | Block 4 (`FormatoPlanillaNoEsperado`), Block 5 (400 con mensaje), Block 6 |
| FR-16 | Block 4 (celdas numéricas con hasta 2 decimales), Block 1 (rango del costo), Block 3 |
| FR-17 | Block 3 (resultado por fila), Block 5, Block 6 (tabla de resultados) |
| FR-18 | Block 3 (indicación de negativo o cero), Block 6 |
| FR-19 | Block 1 (`Desactivar`), Block 3, Block 5, Block 6 |
| FR-20 | Block 1 (`Reactivar`), Block 3, Block 5, Block 6 |
| FR-21 | Block 3 (`ImportarCostos` no toca `Activo`) |
| FR-22 | Block 1 (`TextoBusqueda`), Block 2 (`Buscar` con LIKE escapado), Block 3, Block 5, Block 6 |
| FR-23 | Block 1 (validación del margen), Block 2 (CHECK), Block 5 (400 por campo), Block 6 |
| FR-24 | Block 1 (validación del costo), Block 2 (CHECK), Block 5, Block 6 |
| NFR-01 | Estrategia: cruce de la planilla en memoria con un diccionario `CodigoProveedorClave → artículos`, una sola transacción y guardado por lotes de 500 con varios `SaveChanges`. Lo prueba un test de integración con 10.000 artículos y 10.000 filas, con tope de 120 s y la medición registrada (Block 3 y Block 5). |
| NFR-02 | Estrategia: `decimal` en dominio y aplicación, sin `double`. El costo y el margen tienen como máximo 2 decimales, por eso el redondeo a 6 decimales del resultado intermedio es exacto. Se persiste ×10.000 según ADR-001. Ver los tests de Block 1. |
| NFR-03 | Estrategia: suite Playwright con los proyectos `chrome` y `msedge` (Block 7), corrida en la versión vigente y en la anterior como en FEAT-001a |

| AC | Tests (bloque) |
|---|---|
| AC-01 | B2 `Repositorio_Crear_AsignaCodigoAutonumerico`; B3 `CrearArticulo_Valido_GuardaConPrecioCalculado`; B5 `Articulos_Post_Devuelve201ConCodigoYPrecio`; B7 `alta de artículo` |
| AC-02 | B1 `Calculadora_RI_Costo1000Margen50_Da1500` |
| AC-03 | B1 `Calculadora_RI_Costo1210Margen50_Da1815_ConIntermediosExactos` |
| AC-04 | B1 `Calculadora_Monotributo_Costo1210Margen50_Da1815` |
| AC-05 | B1 `Calculadora_Multiplo50_181537_Da1850` |
| AC-06 | B1 `Calculadora_Multiplo001_1666656_Da166666` |
| AC-07 | B6 `Catalogo_precioVentaEsSoloLectura`; B5 `Articulos_PutConPrecioVenta_Devuelve400` |
| AC-08 | B1 `Articulo_CambiarMargen60_Recalcula1936`; B3 `ModificarArticulo_Margen60_Guarda1936` |
| AC-09 | B1 `Articulo_CambiarCosto2420_Recalcula3630`; B3 `ModificarArticulo_Costo2420_Guarda3630` |
| AC-10 | B3 `CrearArticulo_DespuesDeAlicuota105_Guarda165750` |
| AC-11 | B3 `ActualizarConfiguracion_CambiaMultiplo_RecalculaCatalogo`; B5 `Configuracion_PutMultiplo50_RecalculaArticulos`; B7 `cambio de múltiplo recalcula el catálogo` |
| AC-12 | B3 `ImportarCostos_FilaValida_ActualizaCostoYVenta`; B5 `Planilla_Valida_Actualiza`; B7 `importación de planilla` |
| AC-13 | B4 `Lector_EncabezadosConMayusculasYAcentos_Acepta` |
| AC-14 | B3 `ImportarCostos_CodigoEnMinusculas_Coincide` |
| AC-15 | B3 `ImportarCostos_CodigoConEspacios_Coincide` |
| AC-16 | B4 `Lector_TerceraColumna_FormatoNoEsperado`; B5 `Planilla_TerceraColumna_Devuelve400YNoActualiza` |
| AC-17 | B4 `Lector_Csv_FormatoNoEsperado`, `Lector_EncabezadosDistintos_FormatoNoEsperado`; B6 `ImportarPlanilla_formatoNoEsperado_muestraMensaje` |
| AC-18 | B3 `ImportarCostos_CodigoInexistente_ListaYActualizaElResto` |
| AC-19 | B4 `Lector_PrecioVacioOTexto_FilaInvalida`; B3 `ImportarCostos_PrecioInvalido_Lista` |
| AC-20 | B3 `ImportarCostos_NegativoYCero_ActualizaYLista` |
| AC-21 | B1 `Articulo_Desactivar_ConservaDatos`; B5 `Articulos_Desactivar_QuedaInactivo` |
| AC-22 | B1 `Articulo_Reactivar_QuedaActivo`; B6 `Catalogo_reactivar_muestraActivo` |
| AC-23 | B3 `ImportarCostos_ArticuloInactivo_ActualizaSinReactivar` |
| AC-24 | B2 `Repositorio_Buscar_SinAcentosNiMayusculas_DevuelveActivosEInactivos`; B5 `Articulos_Buscar_lente_DevuelveAmbos`; B6 `Catalogo_busqueda_muestraEstado` |
| AC-25 | B5 `Planilla_10000Filas_MenosDe120Segundos` |
| AC-26 | B1 `Calculadora_Exacto166666_NoSube` |
| AC-27 | B7 suite completa en `chrome` y `msedge` |
| AC-28 | B1 `Articulo_Margen1000_Acepta`; B5 `Articulos_PostMargen1000_Devuelve201` |
| AC-29 | B1 `Articulo_Margen100001_Rechaza`; B5 `Articulos_PostMargen100001_Devuelve400ConCampo`; B6 `Catalogo_margenFueraDeRango_muestraMensaje` |
| AC-30 | B1 `Articulo_MargenNegativo_Rechaza` |
| AC-31 | B1 `Articulo_Costo100000000_Acepta` |
| AC-32 | B1 `Articulo_Costo10000000001_Rechaza`; B5 `Articulos_PostCostoFueraDeRango_Devuelve400ConCampo` |
| AC-33 | B4 `Lector_PrecioFueraDeRango_FilaInvalida`; B3 `ImportarCostos_PrecioFueraDeRango_ListaYActualizaElResto` |

## Dependencies between blocks

- Block 1: ninguna.
- Block 2: depende de Block 1.
- Block 3: depende de Block 1. Sus tests usan dobles en memoria, no Block 2.
- Block 4: depende de Block 3, porque implementa el puerto `ILectorPlanillaCostos` y usa sus tipos de resultado.
- Block 5: depende de Blocks 2, 3 y 4.
- Block 6: depende de Block 5 (contrato HTTP).
- Block 7: depende de Blocks 5 y 6.

Orden de ejecución: 1 → 2 → 3 → 4 → 5 → 6 → 7. Los Blocks 2 y 3 se pueden hacer en cualquier orden entre sí.

---

## Block 1 — Dominio del catálogo y cálculo del precio

**Files**
- `src/Optica.Domain/Catalogo/Articulo.cs` (new). Entidad con `Codigo`, `CodigoProveedor`, `CodigoProveedorClave`, `Descripcion`, `TextoBusqueda`, `PrecioCosto`, `MargenUtilidad`, `PrecioVenta` y `Activo`. Métodos: `Crear(...)`, `Modificar(codigoProveedor, descripcion, costo, margen, config)`, `CambiarCosto(costo, config)`, `RecalcularPrecio(config)`, `Desactivar()` y `Reactivar()`.
- `src/Optica.Domain/Catalogo/CalculadoraPrecioVenta.cs` (new). Servicio puro y estático: `Calcular(costo, margen, ConfiguracionNegocio) → decimal`.
- `src/Optica.Domain/Catalogo/RecalculoPrecios.cs` (new). `Aplicar(IEnumerable<Articulo>, ConfiguracionNegocio) → int` (cantidad recalculada).
- `src/Optica.Domain/Catalogo/TextoNormalizado.cs` (new). `ClaveCodigoProveedor(string)` (trim y `ToLowerInvariant`) y `ParaBusqueda(string)` (trim, minúsculas y sin diacríticos, con `NormalizationForm.FormD` filtrando `NonSpacingMark`).
- `src/Optica.Domain/Catalogo/IRepositorioArticulos.cs` (new). Métodos: `Obtener(int codigo)`, `Agregar(Articulo)`, `Guardar(Articulo)`, `ObtenerTodos()`, `GuardarLote(IReadOnlyCollection<Articulo>)`, `Buscar(string textoBusqueda, int maximo)` y `ObtenerPorClaves(IReadOnlySet<string>)`.
- `tests/Optica.Domain.Tests/CalculadoraPrecioVentaTests.cs` (new)
- `tests/Optica.Domain.Tests/ArticuloTests.cs` (new)
- `tests/Optica.Domain.Tests/TextoNormalizadoTests.cs` (new)

**Logic**
- **Responsable Inscripto (FR-04):**
  - `costoSinIva = costo / (1 + alicuota/100)`
  - `ventaSinIva = costoSinIva × (1 + margen/100)`
  - `bruto = ventaSinIva × (1 + alicuota/100)`
- **Monotributo (FR-05):** `bruto = costo × (1 + margen/100)`.
- **Redondeo (FR-06):**
  - `exacto = Math.Round(bruto, 6, MidpointRounding.AwayFromZero)`, con `múltiplo = config.MultiploRedondeo`.
  - `precio = Math.Ceiling(exacto / múltiplo) × múltiplo`, y después `Math.Round(precio, 2)`.
  - Si `exacto ≤ 0`, se usa el mismo techo; para valores negativos, techo significa hacia cero.
- **Por qué el redondeo a 6 decimales es exacto (NFR-02):** el costo y el margen tienen como máximo 2 decimales, así que el valor matemático `costo × (1 + margen/100)` tiene como máximo 6. Los restos de la división por `(1 + alícuota/100)` quedan por debajo de 10⁻²⁰ y desaparecen al redondear a 6 decimales. En Responsable Inscripto la alícuota se cancela, por eso RI y Monotributo dan el mismo precio, como confirman AC-03 y AC-04.
- **Validación**, con `ErroresValidacion` por campo y mismo estilo que `ConfiguracionNegocio`:
  - `codigoProveedor`: obligatorio, entre 1 y 50 caracteres después del trim.
  - `descripcion`: obligatoria, entre 1 y 200 caracteres después del trim.
  - `precioCosto`: |valor| ≤ 100.000.000, con mensaje "El precio de costo no puede superar $100.000.000" (FR-24). Como máximo 2 decimales, con mensaje "Ingresá como máximo 2 decimales".
  - `margenUtilidad`: entre 0 y 1.000, con mensaje "El margen debe estar entre 0 y 1.000" (FR-23). Como máximo 2 decimales.
- **Activación:** `Desactivar` y `Reactivar` solo cambian `Activo` (FR-19 y FR-20).
- **`CambiarCosto`:** lo usa la planilla. Recalcula el precio y no toca `Activo` (FR-21).

**Error handling**
- E1-1: un campo inválido lanza `ErroresValidacion`. No se modifica nada del artículo.
- E1-2: `RecalcularPrecio` con una configuración nula lanza `ArgumentNullException` (error de programación).

**Required tests**
- [ ] `Calculadora_RI_Costo1000Margen50_Da1500`: AC-02, incluye el caso que con techo directo daría 1.500,01.
- [ ] `Calculadora_RI_Costo1210Margen50_Da1815_ConIntermediosExactos`: AC-03.
- [ ] `Calculadora_Monotributo_Costo1210Margen50_Da1815`: AC-04.
- [ ] `Calculadora_Multiplo50_181537_Da1850`: AC-05.
- [ ] `Calculadora_Multiplo001_1666656_Da166666`: AC-06.
- [ ] `Calculadora_Exacto166666_NoSube`: AC-26.
- [ ] `Calculadora_RI_Alicuota105_Costo1105Margen50_Da165750`: AC-10, a nivel dominio.
- [ ] `Calculadora_CostoNegativoYCero_NoLanza`: FR-16, el precio queda ≤ 0.
- [ ] `Articulo_CambiarMargen60_Recalcula1936`: AC-08.
- [ ] `Articulo_CambiarCosto2420_Recalcula3630`: AC-09.
- [ ] `Articulo_Desactivar_ConservaDatos`: AC-21.
- [ ] `Articulo_Reactivar_QuedaActivo`: AC-22.
- [ ] `Articulo_Margen1000_Acepta`: AC-28.
- [ ] `Articulo_Margen100001_Rechaza`: AC-29.
- [ ] `Articulo_MargenNegativo_Rechaza`: AC-30.
- [ ] `Articulo_Costo100000000_Acepta`: AC-31.
- [ ] `Articulo_Costo10000000001_Rechaza`: AC-32.
- [ ] `Articulo_MasDeDosDecimales_Rechaza`: sad path, costo 1,234 y margen 50,555.
- [ ] `Articulo_DescripcionVaciaOCodigoLargo_Rechaza`: sad path.
- [ ] `RecalculoPrecios_Aplicar_RecalculaActivosEInactivos`: FR-10.
- [ ] `TextoNormalizado_Clave_ConservaAcentos`: FR-13, "AÑO-1" y "ANO-1" dan claves distintas.
- [ ] `TextoNormalizado_Busqueda_QuitaAcentosYMayusculas`: FR-22.
- [ ] `Articulo_RecalcularSinConfiguracion_LanzaArgumentNull`: E1-2.

**Completion criterion**
`dotnet test tests/Optica.Domain.Tests` pasa todos los tests, los 26 anteriores más los nuevos, y `Optica.Domain.csproj` sigue sin paquetes.

---

## Block 2 — Persistencia del catálogo

**Files**
- `src/Optica.Infrastructure/Persistencia/OpticaDbContext.cs` (modified). Mapeo de `Articulo`, con el conversor ×10.000 de ADR-001 para los decimales.
- `src/Optica.Infrastructure/Persistencia/RepositorioArticulos.cs` (new)
- `src/Optica.Infrastructure/Persistencia/Migrations/*_Catalogo.cs` (new) más su `.Designer.cs` y `OpticaDbContextModelSnapshot.cs` (modified)
- `src/Optica.Infrastructure/RegistroInfraestructura.cs` (modified). Registra `IRepositorioArticulos`.
- `tests/Optica.Infrastructure.Tests/RepositorioArticulosTests.cs` (new)
- `tests/Optica.Infrastructure.Tests/RegistroYErroresTests.cs` (modified). `Assert.Single` sobre las migraciones pasa a verificar `AccesoYConfiguracion` y `Catalogo`.

**Data model**

Tabla `Articulos`:

| Columna | Tipo | Restricciones |
|---|---|---|
| `Codigo` | INTEGER | PK AUTOINCREMENT (FR-02) |
| `CodigoProveedor` | TEXT | NOT NULL, `CHECK (length(CodigoProveedor) BETWEEN 1 AND 50)` |
| `CodigoProveedorClave` | TEXT | NOT NULL. Índice no único `IX_Articulos_CodigoProveedorClave` (S-02: no es único) |
| `Descripcion` | TEXT | NOT NULL, `CHECK (length(Descripcion) BETWEEN 1 AND 200)` |
| `TextoBusqueda` | TEXT | NOT NULL. Texto normalizado `codigo proveedor + descripcion` |
| `PrecioCosto` | INTEGER | NOT NULL, ×10.000, `CHECK (PrecioCosto BETWEEN -1000000000000 AND 1000000000000)` |
| `MargenUtilidad` | INTEGER | NOT NULL, ×10.000, `CHECK (MargenUtilidad BETWEEN 0 AND 10000000)` |
| `PrecioVenta` | INTEGER | NOT NULL, ×10.000 |
| `Activo` | INTEGER | NOT NULL, DEFAULT 1, `CHECK (Activo IN (0,1))` |

**Logic**
- `Buscar(texto, maximo)`:
  - Hace `WHERE TextoBusqueda LIKE @patron ESCAPE '\'` y, si el texto es un número entero, `OR Codigo = @codigo`.
  - El patrón se arma escapando `\`, `%` y `_`, y siempre va como parámetro.
  - Ordena por `Descripcion` y aplica `LIMIT @maximo`.
- `ObtenerPorClaves`: hace `WHERE CodigoProveedorClave IN (...)`, en lotes de 500 parámetros.
- `GuardarLote`: llama a `SaveChanges` cada 500 entidades dentro de la transacción ambiente de `UnidadDeTrabajo`. No abre transacciones propias y no hace `Detach` por entidad.
- `Agregar` devuelve el `Codigo` generado.

**Error handling**
- E2-1: si un CHECK falla, se lanza `DbUpdateException` y la transacción se revierte. No debería pasar, porque el dominio valida antes.
- E2-2: si la base está ocupada, se propaga una `SqliteException` con código 5, que ya se traduce a 503.

**Required tests**
- [ ] `Repositorio_Crear_AsignaCodigoAutonumerico`: AC-01, FR-02.
- [ ] `Repositorio_GuardaYLee_ImportesEscalados`: ADR-001, con los valores 1.815,00, 1.000 y -100.
- [ ] `Repositorio_Buscar_SinAcentosNiMayusculas_DevuelveActivosEInactivos`: AC-24.
- [ ] `Repositorio_Buscar_PorcentajeYGuionBajo_SeEscapan`: sad path de inyección en LIKE.
- [ ] `Repositorio_Buscar_RespetaElMaximo`
- [ ] `Repositorio_ObtenerPorClaves_DevuelveTodosLosQueComparten`: S-02.
- [ ] `Repositorio_GuardarLote_ExcepcionRevierteTodo`: FR-10, sin quedar a medio actualizar.
- [ ] `Migracion_Catalogo_CreaTablaConChecks`: sad path, un margen de 1.001 directo en SQL viola el CHECK.
- [ ] `Migraciones_AplicadasEnOrden`: reemplaza el `Assert.Single`.
- [ ] `Repositorio_GuardarLote_BaseOcupada_PropagaSqliteException`: E2-2.

**Completion criterion**
`dotnet test tests/Optica.Infrastructure.Tests` pasa todos los tests y `dotnet tool run dotnet-ef migrations list --project src/Optica.Infrastructure` muestra `Catalogo`.

---

## Block 3 — Casos de uso del catálogo, importación y recálculo

**Files**
- `src/Optica.Application/Catalogo/CrearArticulo.cs` (new)
- `src/Optica.Application/Catalogo/ModificarArticulo.cs` (new)
- `src/Optica.Application/Catalogo/ObtenerArticulo.cs` (new). Incluye el record `DatosArticulo`.
- `src/Optica.Application/Catalogo/BuscarArticulos.cs` (new). Tope de 200 resultados.
- `src/Optica.Application/Catalogo/DesactivarArticulo.cs` y `ReactivarArticulo.cs` (new)
- `src/Optica.Application/Catalogo/ImportarCostos.cs` (new)
- `src/Optica.Application/Catalogo/ILectorPlanillaCostos.cs` (new). Puerto `Leer(Stream) → PlanillaCostos`. Si el formato no es el esperado, lanza `FormatoPlanillaNoEsperado`.
- `src/Optica.Application/Catalogo/PlanillaCostos.cs` (new). Contiene `FilaPlanilla(int numeroFila, string codigoProveedor, decimal? precio, bool precioValido)` y `ResultadoImportacion(int actualizados, IReadOnlyList<FilaNoActualizada>, IReadOnlyList<FilaObservada>)`.
- `src/Optica.Application/Configuracion/ActualizarConfiguracion.cs` (modified). Recibe `IRepositorioArticulos` y, dentro de su unidad de trabajo, recalcula con `RecalculoPrecios` y guarda con `GuardarLote`.
- `src/Optica.Application/Comun/Errores.cs` (modified). Agrega `ArticuloInexistente(int codigo)` y `FormatoPlanillaNoEsperado`, con el mensaje fijo de AC-16 y sin contenido de celdas.
- `src/Optica.Application/RegistroAplicacion.cs` (modified). Registra los 7 casos de uso nuevos.
- `tests/Optica.Application.Tests/Dobles/RepositorioArticulosEnMemoria.cs` (new). Implementa `IParticipanteTransaccion`.
- `tests/Optica.Application.Tests/Dobles/LectorPlanillaFalso.cs` (new)
- `tests/Optica.Application.Tests/Dobles/UnidadDeTrabajoInmediata.cs` (modified, si hace falta para varios participantes)
- `tests/Optica.Application.Tests/CatalogoTests.cs` (new)
- `tests/Optica.Application.Tests/ImportarCostosTests.cs` (new)
- `tests/Optica.Application.Tests/ConfiguracionTests.cs` (modified). Nuevo constructor y test de AC-11.

**Logic**
- **`CrearArticulo` y `ModificarArticulo`:** dentro de `IUnidadDeTrabajo`, leen la configuración, construyen o modifican el `Articulo` (que calcula el precio) y lo guardan.
- **`ImportarCostos`:**
  1. Lee la planilla con el lector **antes** de abrir la unidad de trabajo, así el lock no se toma mientras se parsea.
  2. Abre la unidad de trabajo, obtiene la configuración y llama a `ObtenerPorClaves` con todas las claves de la planilla.
  3. Arma un diccionario `clave → lista de artículos`. Recorre las filas en orden:
     - si el precio es inválido, va a `FilaNoActualizada(numero, codigo, "precio inválido")`;
     - si la clave no existe, va a `FilaNoActualizada(numero, codigo, "código inexistente")`;
     - si existe, se llama a `CambiarCosto` en **todos** los artículos con esa clave (S-02), y si el precio es ≤ 0 la fila se agrega a `FilaObservada(numero, codigo, "actualizado con precio negativo o cero")`.
  4. Llama a `GuardarLote`.
- **`ActualizarConfiguracion`:** solo si cambian `AlicuotaIva`, `CondicionFiscal` o `MultiploRedondeo`, carga todos los artículos, aplica `RecalculoPrecios` y llama a `GuardarLote`, todo en la **misma** unidad de trabajo que la configuración (FR-10).
- **`BuscarArticulos`:** normaliza el texto con `TextoNormalizado.ParaBusqueda` y llama a `Buscar(texto, 200)`.
- Ningún caso de uso registra en logs. Los eventos los emite la API, solo con cantidades.

**Error handling**
- E3-1: un artículo inexistente en la edición, activación o desactivación lanza `ArticuloInexistente`.
- E3-2: una validación lanza `ErroresValidacion`, que se propaga.
- E3-3: si el formato de la planilla no es el esperado, se propaga `FormatoPlanillaNoEsperado` y no se abre la unidad de trabajo (FR-14).
- E3-4: un fallo de persistencia en el recálculo revierte también la configuración. No queda configuración nueva con precios viejos.

**Required tests**
- [ ] `CrearArticulo_Valido_GuardaConPrecioCalculado`: AC-01.
- [ ] `CrearArticulo_DespuesDeAlicuota105_Guarda165750`: AC-10.
- [ ] `ModificarArticulo_Margen60_Guarda1936`: AC-08.
- [ ] `ModificarArticulo_Costo2420_Guarda3630`: AC-09.
- [ ] `ModificarArticulo_Inexistente_LanzaArticuloInexistente`: sad path, E3-1.
- [ ] `CrearArticulo_Invalido_PropagaErroresValidacionSinGuardar`: E3-2.
- [ ] `ActualizarConfiguracion_CambiaMultiplo_RecalculaCatalogo`: AC-11.
- [ ] `ActualizarConfiguracion_SoloTope_NoRecalcula`
- [ ] `ActualizarConfiguracion_FallaGuardarArticulos_RevierteConfiguracion`: E3-4.
- [ ] `ImportarCostos_FilaValida_ActualizaCostoYVenta`: AC-12.
- [ ] `ImportarCostos_CodigoEnMinusculas_Coincide`: AC-14.
- [ ] `ImportarCostos_CodigoConEspacios_Coincide`: AC-15.
- [ ] `ImportarCostos_CodigoConAcentoDistinto_NoCoincide`: FR-13, decisión del usuario.
- [ ] `ImportarCostos_CodigoInexistente_ListaYActualizaElResto`: AC-18.
- [ ] `ImportarCostos_PrecioInvalido_Lista`: AC-19.
- [ ] `ImportarCostos_NegativoYCero_ActualizaYLista`: AC-20.
- [ ] `ImportarCostos_ArticuloInactivo_ActualizaSinReactivar`: AC-23.
- [ ] `ImportarCostos_PrecioFueraDeRango_ListaYActualizaElResto`: AC-33.
- [ ] `ImportarCostos_CodigoCompartido_ActualizaLosDos`: S-02.
- [ ] `ImportarCostos_FormatoNoEsperado_NoAbreUnidadDeTrabajo`: E3-3.
- [ ] `BuscarArticulos_TextoConAcentos_Normaliza`: FR-22.

**Completion criterion**
`dotnet test tests/Optica.Application.Tests` pasa todos los tests sin SQLite, y `Optica.Application.csproj` sigue referenciando solo a Domain.

---

## Block 4 — Lector de planillas .xlsx

**Files**
- `src/Optica.Infrastructure/Optica.Infrastructure.csproj` (modified). Agrega `DocumentFormat.OpenXml` con versión exacta, estable y publicada hace al menos 2 semanas.
- Lockfiles `packages.lock.json` de `src/Optica.Infrastructure`, `src/Optica.Api`, `tests/Optica.Infrastructure.Tests` y `tests/Optica.Api.Tests` (modified).
- `src/Optica.Infrastructure/Planillas/LectorPlanillaCostosXlsx.cs` (new). Implementa `ILectorPlanillaCostos`.
- `src/Optica.Infrastructure/Planillas/InspectorZip.cs` (new). Valida el ZIP antes de abrirlo con OpenXml.
- `src/Optica.Infrastructure/RegistroInfraestructura.cs` (modified). Registra el lector.
- `docs/adr/adr-002-lector-xlsx-openxml.md` (new)
- `tests/Optica.Infrastructure.Tests/LectorPlanillaCostosXlsxTests.cs` (new)
- `tests/Optica.Infrastructure.Tests/Planillas/GeneradorPlanillas.cs` (new). Genera los `.xlsx` de prueba en memoria con OpenXml, sin binarios en el repo.

**Logic**
1. **`InspectorZip` sobre el `Stream`, que ya está en memoria:**
   - firma ZIP `PK\x03\x04`; si no la tiene, el formato no es el esperado;
   - como máximo 100 entradas;
   - tamaño descomprimido total ≤ 20 MB y por entrada ≤ 10 MB;
   - relación de compresión ≤ 100:1 por entrada;
   - no puede tener `xl/vbaProject.bin` (rechaza `.xlsm`);
   - `[Content_Types].xml` tiene que declarar `application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml`.
2. **Apertura y lectura:** `SpreadsheetDocument.Open(stream, false)`. Lee la **primera hoja** en el orden de `workbook.xml` con `OpenXmlReader` (SAX). Resuelve los strings compartidos y no evalúa fórmulas.
3. **Encabezados de la fila 1:** tiene que haber exactamente dos celdas no vacías, en A1 y B1. Se normalizan con trim, minúsculas y sin acentos, y se comparan con `codigo proveedor` y `precio costo`. Cualquier otra cosa da formato no esperado (FR-11 y FR-14).
4. **Filas de datos, desde la 2:**
   - Una fila completamente vacía se ignora.
   - Si hay más de 10.000 filas de datos, el formato no es el esperado.
   - Una celda con datos en la columna C o posterior da formato no esperado.
5. **Precio (FR-16):**
   - Es válido solo si la celda es numérica (sin `t` o con `t="n"`), se parsea con `decimal.Parse` en cultura invariante, tiene como máximo 2 decimales y está dentro de ±100.000.000. La constante sale del dominio.
   - Una celda vacía, de texto, de fórmula sin valor guardado, de error o booleana da `precioValido = false`.
6. **Código:** se toma el texto de la celda A tal como está. Si está vacío y el precio existe, la fila se marca como código inexistente en la aplicación.

**Input validation**
- El stream tiene que estar en memoria, con tamaño ≤ 2 MB. Lo garantiza la API.
- No se resuelven DTD. OpenXml usa `XmlReader` con `DtdProcessing.Prohibit`, y hay un test que lo verifica.

**Error handling**
- E4-1: toda violación de formato o de los límites del ZIP lanza `FormatoPlanillaNoEsperado`, con mensaje fijo y **sin** contenido de celdas ni nombre de archivo.
- E4-2: una excepción interna de OpenXml o de un XML mal formado se captura y se convierte en `FormatoPlanillaNoEsperado`. El detalle original no se loguea.

**Required tests**
- [ ] `Lector_EncabezadosConMayusculasYAcentos_Acepta`: AC-13.
- [ ] `Lector_TerceraColumna_FormatoNoEsperado`: AC-16.
- [ ] `Lector_Csv_FormatoNoEsperado`: AC-17.
- [ ] `Lector_EncabezadosDistintos_FormatoNoEsperado`: AC-17.
- [ ] `Lector_PrecioVacioOTexto_FilaInvalida`: AC-19.
- [ ] `Lector_PrecioConTresDecimales_FilaInvalida`: decisión del usuario.
- [ ] `Lector_PrecioFueraDeRango_FilaInvalida`: AC-33.
- [ ] `Lector_FormulaSinValor_FilaInvalida`
- [ ] `Lector_PrimeraHojaEnOrdenDelLibro`: FR-11.
- [ ] `Lector_MasDe10000Filas_FormatoNoEsperado`
- [ ] `InspectorZip_RelacionDeCompresionExcesiva_Rechaza`: ZIP bomb.
- [ ] `InspectorZip_ConVbaProject_Rechaza`: `.xlsm`.
- [ ] `Lector_XmlConDtd_Rechaza`: XXE.
- [ ] `Lector_Excepcion_NoIncluyeContenidoDeCeldas`: datos confidenciales.
- [ ] `Lector_XmlMalFormado_FormatoNoEsperado`: E4-2.

**Completion criterion**
`dotnet test tests/Optica.Infrastructure.Tests` pasa todos los tests, `dotnet list package --vulnerable --include-transitive` da 0 vulnerabilidades y el ADR-002 está escrito.

---

## Block 5 — API del catálogo y de la planilla

**Files**
- `src/Optica.Api/Controllers/ArticulosController.cs` (new). Endpoints de artículos, sin lógica.
- `src/Optica.Api/Controllers/PlanillaCostosController.cs` (new). `POST /api/articulos/planilla`.
- `src/Optica.Api/Seguridad/LimiteCuerpo.cs` (modified). Límites por ruta: 4 KB por defecto para `/api/*` y 2 MB para `/api/articulos/planilla`.
- `src/Optica.Api/Seguridad/EncabezadosSeguridad.cs` (modified). `Cache-Control: no-store` en `/api/*`.
- `src/Optica.Api/Errores/ManejadorErrores.cs` (modified). Traduce `ArticuloInexistente` a 404 y `FormatoPlanillaNoEsperado` a 400 con su mensaje, y el título del 413 deja de decir "4 KB".
- `src/Optica.Api/EventosOptica.cs` (modified). Eventos `ArticuloCreado`, `ArticuloModificado`, `ArticuloDesactivado`, `ArticuloReactivado`, `PlanillaImportada(actualizados, noActualizados, observados)` y `CatalogoRecalculado(cantidad)`, solo con cantidades y el código numérico interno.
- `src/Optica.Api/Controllers/ConfiguracionController.cs` (modified). Emite `CatalogoRecalculado` cuando corresponde.
- `tests/Optica.Api.Tests/FabricaApi.cs` (modified). Ayudantes multipart y de artículos.
- `tests/Optica.Api.Tests/ArticulosApiTests.cs` (new)
- `tests/Optica.Api.Tests/PlanillaApiTests.cs` (new)
- `tests/Optica.Api.Tests/SeguridadApiTests.cs` (modified):
  - suma los endpoints nuevos a `EndpointsDocumentados`;
  - agrega `ArticulosController.Buscar(texto)` a `ParametrosTextoPermitidos`, con la justificación "texto de búsqueda sin datos personales, logueado solo en Trace";
  - suma casos al 413.
- `tests/Optica.Api.Tests/ConfiguracionApiTests.cs` (modified). Test de AC-11 de punta a punta en la API.

**API contract**

Todos los endpoints exigen sesión. Los POST y PUT exigen antiforgery. Los errores responden `application/problem+json` como en FEAT-001a.

1. `GET /api/articulos?texto={texto}`
   - Respuesta 200: `{ articulos: [{ codigo, codigoProveedor, descripcion, precioCosto, margenUtilidad, precioVenta, activo }], truncado: boolean }`, con 200 resultados como máximo.
   - `texto` admite hasta 100 caracteres; si está vacío, devuelve los primeros 200 por descripción.
   - Errores: 400 si `texto` supera los 100 caracteres; 401.
2. `GET /api/articulos/{codigo}`
   - Respuesta 200: el mismo objeto.
   - Errores: 404 `{ title: "El artículo no existe" }`; 401.
3. `POST /api/articulos`
   - Request: `{ codigoProveedor: string, descripcion: string, precioCosto: number, margenUtilidad: number }`.
   - Respuesta 201: el objeto con `codigo` y `precioVenta`.
   - Errores: 400 con `errors.<campo>` (Block 1); 400 si viene un campo de más, por ejemplo `precioVenta` o `codigo` (JSON estricto, AC-07); 401.
4. `PUT /api/articulos/{codigo}`
   - Request: el mismo cuerpo que el POST.
   - Respuesta 200: el objeto actualizado.
   - Errores: 400, 404 y 401.
5. `POST /api/articulos/{codigo}/desactivar` y `POST /api/articulos/{codigo}/reactivar`
   - Respuesta 200: el objeto.
   - Errores: 404 y 401.
6. `POST /api/articulos/planilla`
   - Request: `multipart/form-data` con un único campo `archivo` de hasta 2 MB.
   - Respuesta 200: `{ actualizados: number, noActualizados: [{ fila: number, codigoProveedor: string, razon: "código inexistente" | "precio inválido" }], observados: [{ fila, codigoProveedor, indicacion: "actualizado con precio negativo o cero" }] }`.
   - Errores:
     - 400 `{ title: "El formato de la planilla no es el esperado: la primera fila debe tener solo las columnas Codigo proveedor y Precio costo" }`;
     - 400 si falta `archivo` o vienen varios archivos;
     - 413 si supera 2 MB;
     - 401;
     - 503 si la base está ocupada.

**Input validation**
- JSON estricto como en FEAT-001a: un campo de más o un tipo erróneo dan 400 por campo.
- La planilla se lee con `MultipartReader` directo a un `MemoryStream` con tope de 2 MB. **No se usa `IFormFile`**, así no se crean archivos temporales (FAIL-3 de la auditoría).
- `codigo` de la ruta: entero positivo, con restricción de ruta `:int:min(1)`.

**Error handling**
- E5-1: un cuerpo de más de 2 MB en la planilla, o de más de 4 KB en los demás endpoints, da 413.
- E5-2: la falta de antiforgery da 400, como en FEAT-001a, y se valida antes de leer el multipart.
- E5-3: ningún 4xx ni 5xx incluye contenido de la planilla, costos ni márgenes en el log. `ManejadorErrores` no loguea el mensaje de `FormatoPlanillaNoEsperado`.

**Required tests**
- [ ] `Articulos_Post_Devuelve201ConCodigoYPrecio`: AC-01.
- [ ] `Articulos_PutConPrecioVenta_Devuelve400`: AC-07, FR-08.
- [ ] `Articulos_PostMargen1000_Devuelve201`: AC-28.
- [ ] `Articulos_PostMargen100001_Devuelve400ConCampo`: AC-29.
- [ ] `Articulos_PostCostoFueraDeRango_Devuelve400ConCampo`: AC-32.
- [ ] `Articulos_Desactivar_QuedaInactivo`: AC-21.
- [ ] `Articulos_Buscar_lente_DevuelveAmbos`: AC-24.
- [ ] `Articulos_Inexistente_Devuelve404`: sad path.
- [ ] `Articulos_SinSesion_Devuelven401`: lo cubre el test de endpoints documentados.
- [ ] `Configuracion_PutMultiplo50_RecalculaArticulos`: AC-11.
- [ ] `Planilla_Valida_Actualiza`: AC-12.
- [ ] `Planilla_TerceraColumna_Devuelve400YNoActualiza`: AC-16.
- [ ] `Planilla_Mayor2MB_Devuelve413`: sad path, E5-1.
- [ ] `Articulos_CuerpoMayor4KB_Devuelve413`: E5-1.
- [ ] `Planilla_SinAntiforgery_Devuelve400`: sad path.
- [ ] `Planilla_NoCreaArchivosTemporales`: verifica que el directorio temporal no cambia durante la carga.
- [ ] `Planilla_10000Filas_MenosDe120Segundos`: AC-25, NFR-01. Registra el tiempo en la salida del test.
- [ ] `Logs_NoContienenCostosNiCodigos`: datos confidenciales.
- [ ] `Respuestas_Api_IncluyenCacheControlNoStore`

**Completion criterion**
`dotnet test tests/Optica.Api.Tests` pasa todos los tests, los 62 anteriores más los nuevos, y `dotnet build Optica.sln` da 0 warnings.

---

## Block 6 — Interfaz web del catálogo

**Files**
- `web/src/api/cliente.ts` (modified). Admite `FormData` sin fijar `Content-Type`, manteniendo `X-XSRF-TOKEN`. El comentario del encabezado se actualiza para admitir la búsqueda por query string.
- `web/src/api/articulos.ts` (new). Funciones tipadas para los endpoints de Block 5.
- `web/src/paginas/Catalogo.tsx` (new). Búsqueda, tabla con estado, formulario de alta y edición con `CampoConError`, precio de venta de solo lectura, botones Desactivar y Reactivar, y aviso de "resultados truncados".
- `web/src/paginas/ImportarPlanilla.tsx` (new). Input de archivo con `accept=".xlsx"`, envío, y tablas de "no actualizadas" y "observadas".
- `web/src/App.tsx` (modified). El tipo `Pantalla` pasa a separar las rutas públicas de las que requieren sesión. Con sesión se puede entrar a `/catalogo`, `/catalogo/importar` y `/configuracion`, y la pantalla de inicio es `/catalogo`.
- `web/src/componentes/Layout.tsx` (modified). Navegación Catálogo / Importar planilla / Configuración.
- `web/src/paginas/Ingreso.tsx` (modified). Después del 204 navega a `/catalogo`.
- `web/src/pruebas/servidorFalso.ts` (modified). Registra la query string y el cuerpo `FormData`, y en modo estricto distingue los pedidos con distinta query.
- Tests:
  - `web/src/paginas/Catalogo.test.tsx`, `web/src/paginas/ImportarPlanilla.test.tsx` y `web/src/api/articulos.test.ts` (new);
  - `web/src/App.test.tsx`, `web/src/componentes/Layout.test.tsx`, `web/src/paginas/Ingreso.test.tsx` y `web/src/api/cliente.test.tsx` (modified).

**Logic**
- Los números se manejan en formato es-AR con el mismo parseo de FEAT-001a: coma decimal y "Usá coma para los decimales".
- El precio de venta se muestra con `readOnly` y no se envía nunca.
- Los errores por campo se muestran junto a cada campo; un 5xx o la red caída muestran el mensaje genérico y conservan lo cargado.
- Los costos y márgenes no se guardan en `localStorage`, `sessionStorage` ni en la URL. La búsqueda manda solo el texto buscado.
- No se escriben colores a mano; los cubre el test `Marca_sinColoresEscritosEnElCodigo`.

**Input validation**
- Descripción: `maxLength=200`. Código en el proveedor: `maxLength=50`. Búsqueda: `maxLength=100`.
- Costo y margen: `inputMode="decimal"` y parseo es-AR. Los rangos los valida el servidor y el mensaje aparece junto al campo.
- Archivo: `accept=".xlsx"`; además, la UI rechaza archivos de más de 2 MB antes de enviarlos.

**Error handling**
- E6-1: un 400 por campo muestra el mensaje junto al campo.
- E6-2: un 400 de formato de planilla muestra el mensaje del servidor.
- E6-3: un 413 muestra "La planilla supera los 2 MB".
- E6-4: un 401 redirige a ingreso, como en FEAT-001a.

**Required tests**
- [ ] `Catalogo_precioVentaEsSoloLectura`: AC-07.
- [ ] `Catalogo_alta_muestraCodigoYPrecio`: AC-01.
- [ ] `Catalogo_margenFueraDeRango_muestraMensaje`: AC-29.
- [ ] `Catalogo_busqueda_muestraEstado`: AC-24.
- [ ] `Catalogo_reactivar_muestraActivo`: AC-22.
- [ ] `Catalogo_errorDeRed_conservaLoCargado`: sad path.
- [ ] `ImportarPlanilla_resultado_muestraNoActualizadasYObservadas`: AC-18 a AC-20.
- [ ] `ImportarPlanilla_formatoNoEsperado_muestraMensaje`: AC-17.
- [ ] `ImportarPlanilla_archivoMayor2MB_noEnvia`: sad path.
- [ ] `ImportarPlanilla_413_muestraMensaje`: E6-3.
- [ ] `Catalogo_401_redirigeAIngreso`: E6-4.
- [ ] `cliente_formData_noFijaContentTypeYMandaXsrf`
- [ ] `App_conSesion_permiteCatalogoYConfiguracion`
- [ ] `Ingreso_204_vaACatalogo`

**Completion criterion**
`npm run test:coverage` pasa con el umbral del 80 % en las 4 métricas y `npm run build` no da errores.

---

## Block 7 — Pruebas de punta a punta del catálogo

**Files**
- `e2e/tests/catalogo.spec.ts` (new)
- `e2e/tests/soporte.ts` (modified). Ayudantes para fijar la configuración y crear artículos por API, y para generar planillas.
- `e2e/tests/planillas.ts` (new). Genera los `.xlsx` de prueba en tiempo de ejecución, sin binarios en el repo.
- `e2e/package.json` y `e2e/package-lock.json` (modified, solo si hace falta una dependencia para generar `.xlsx`; preferible generarlos con la API de zip de Node, sin dependencia nueva).

**Logic**
- En el `beforeAll`: se blanquea, se crea la contraseña y se fija la configuración en Responsable Inscripto, 21 y 0,01, así el archivo no depende de `configuracion.spec.ts`.
- Flujos:
  - alta de artículo con el precio calculado;
  - edición del margen;
  - desactivación y reactivación;
  - búsqueda;
  - importación de una planilla con filas válidas, código inexistente, precio inválido y precio negativo;
  - cambio del múltiplo en Configuración y verificación del precio recalculado en el catálogo.

**Error handling**
- E7-1: la API rechaza la planilla de prueba con formato válido. El test `importación de planilla` falla mostrando el cuerpo del 400.
- E7-2: la UI no muestra el precio recalculado después de cambiar el múltiplo. Falla `cambio de múltiplo recalcula el catálogo`.
- E7-3: hay una violación de CSP o un error en la consola del navegador. La fixture `consola` de FEAT-001a hace fallar el test correspondiente.

**Required tests**
- [ ] `alta de artículo`: AC-01 y AC-02.
- [ ] `edición recalcula el precio`: AC-08.
- [ ] `desactivar y reactivar`: AC-21 y AC-22.
- [ ] `búsqueda muestra activos e inactivos`: AC-24.
- [ ] `importación de planilla`: AC-12, AC-18, AC-19 y AC-20.
- [ ] `cambio de múltiplo recalcula el catálogo`: AC-11.
- [ ] Toda la suite en los proyectos `chrome` y `msedge`: AC-27.

**Completion criterion**
`npx playwright test` en `e2e/`, corrida desde Windows, pasa la suite completa en `chrome` y `msedge`, y `npm run test:harness` sigue en verde.

---

## Final verification
- `dotnet build Optica.sln` sin warnings y `dotnet test Optica.sln --settings coverlet.runsettings` en verde, con cobertura ≥ 80 % en el código nuevo.
- `npm run test:coverage` y `npm run build` en `web/` en verde.
- `npm run test:harness` y `npx playwright test` en `e2e/` en verde en `chrome` y `msedge`.
- Reglas de dependencia: EF y SQLite solo en Infrastructure; DocumentFormat.OpenXml solo en Infrastructure; Domain sin paquetes.
- Los AC-01 a AC-33 tienen al menos un test en verde, según la tabla de cobertura.
- Las mitigaciones de `docs/daw/security/threat-FEAT-001b.md` están implementadas.
- Durante la carga de la planilla no se crean archivos temporales, y ningún log contiene costos, márgenes, códigos de proveedor ni contenido de celdas.
- `git status` no muestra `*.db*`, `.xlsx` ni claves.
- `dotnet list package --vulnerable` y `npm audit` dan 0 vulnerabilidades.
