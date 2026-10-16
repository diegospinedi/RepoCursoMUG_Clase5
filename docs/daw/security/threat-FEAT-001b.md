# Threat model — FEAT-001b: Catálogo de artículos y cálculo del precio de venta

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001b |
| Spec | docs/daw/specs/spec-FEAT-001b.md |
| PRD | docs/daw/prd/prd-FEAT-001b.md |
| Base | Threat model de FEAT-001a (`docs/daw/security/threat-FEAT-001a.md`); sus mitigaciones y riesgos aceptados AR-01 a AR-04 siguen vigentes |
| Fecha | 2026-10-16 |

## 1. Arquitectura analizada

| Id | Componente | Archivos de la spec |
|---|---|---|
| C1 | Pantallas Catálogo e Importar planilla | `web/src/paginas/Catalogo.tsx`, `ImportarPlanilla.tsx`, `web/src/api/articulos.ts`, `cliente.ts` (con `FormData`) |
| C2 | API del catálogo | `ArticulosController` (6 endpoints), `PlanillaCostosController` (`POST /api/articulos/planilla`), `LimiteCuerpo` por ruta, `EncabezadosSeguridad` (`Cache-Control: no-store`), `ManejadorErrores`, `EventosOptica` |
| C3 | Casos de uso y dominio | `CrearArticulo`, `ModificarArticulo`, `BuscarArticulos`, `ImportarCostos`, `ActualizarConfiguracion` (recálculo), `Articulo`, `CalculadoraPrecioVenta`, `RecalculoPrecios` |
| C4 | Lector de planillas | `LectorPlanillaCostosXlsx`, `InspectorZip`, DocumentFormat.OpenXml |
| C5 | Persistencia | Tabla `Articulos` con CHECK, `RepositorioArticulos` (`Buscar` con LIKE escapado, `GuardarLote`) |
| C6 | Logs | Eventos `ArticuloCreado`, `ArticuloModificado`, `ArticuloDesactivado`, `ArticuloReactivado`, `PlanillaImportada` y `CatalogoRecalculado`, más el Visor de eventos |

**Flujo de la planilla:**
1. Navegador (C1).
2. `POST multipart` por loopback, con antiforgery y sesión.
3. `LimiteCuerpo` (2 MB).
4. `MultipartReader` lee el archivo a un `MemoryStream`, sin disco.
5. `ImportarCostos`.
6. `ILectorPlanillaCostos`: `InspectorZip` y lectura SAX.
7. Unidad de trabajo (`BEGIN IMMEDIATE`), cruce por `CodigoProveedorClave` y `GuardarLote`.
8. Respuesta con fila, código y razón.

## 2. Fronteras de confianza (F-TM-02)

| Id | Frontera | Qué la cruza |
|---|---|---|
| TB1 | Navegador → API (loopback, heredada de FEAT-001a) | Pedidos JSON del catálogo y el multipart con el `.xlsx` |
| TB2 | **Archivo externo → proceso** (nueva) | Contenido de la planilla que envía un proveedor. Es dato no confiable aunque lo suba la operadora autenticada: puede venir manipulado (ZIP bomb, XML con DTD, macros, fórmulas) |
| TB3 | API → archivo de base (heredada) | Costos, márgenes y precios persistidos |
| TB4 | Proceso → disco temporal y logs (nueva en este ticket) | Posible volcado del multipart a `%TEMP%` y mensajes de error con contenido de celdas |

## 3. Clasificación de datos (F-TM-05) y cifrado (F-TM-07)

| Dato | Clasificación | En tránsito | En reposo |
|---|---|---|---|
| Precio de costo y margen de utilidad | **Confidencial comercial** (empresa y proveedores; financiero) | Solo por loopback (TB1). Sin TLS: AR-01 vigente | En `optica.db` sin cifrar, con la ACL de FEAT-001a. Riesgo aceptado **AR-05**. No se escribe en temporales, logs ni caché del navegador |
| Planilla `.xlsx` del proveedor | Confidencial comercial | Loopback | **No se persiste:** se procesa en memoria y se descarta |
| Precio de venta | Comercial (público ante el cliente) | Loopback | `optica.db` |
| Código en el proveedor y descripción | Interno | Loopback | `optica.db` |
| Datos personales | **No hay** en este ticket | — | — |
| Credenciales | Sin cambios respecto de FEAT-001a | — | — |

Como este ticket no maneja PII ni credenciales nuevas, F-TM-07 no exige cifrado adicional. El cifrado en reposo de `optica.db` queda pendiente para FEAT-001c (datos personales, Ley 25.326).

## 4. Análisis STRIDE por componente (F-TM-01)

### C1 — Pantallas del catálogo
| STRIDE | Amenaza | Mitigación |
|---|---|---|
| S | Usar la pantalla sin sesión | Guarda de ruteo y 401 de la API (FEAT-001a) |
| T | Enviar `precioVenta` o `codigo` manipulados desde el navegador | El DTO no tiene esos campos y el JSON estricto los rechaza con 400 (AC-07) |
| R | — | Los eventos se registran en la API (C6) |
| I | Costos y márgenes guardados en la caché del navegador o en el storage | `Cache-Control: no-store` en `/api/*`; nada en `localStorage`, `sessionStorage` ni en la URL; la búsqueda solo manda el texto buscado |
| D | Subir un archivo enorme desde la UI | Rechazo en el cliente si pasa de 2 MB; el límite real está en la API |
| E | XSS con una descripción maliciosa | React escapa la salida; no se usa `dangerouslySetInnerHTML`; CSP `default-src 'self'` |

### C2 — API del catálogo
| STRIDE | Amenaza | Mitigación |
|---|---|---|
| S | Endpoints sin autenticación | `FallbackPolicy` autenticada; ningún `[AllowAnonymous]` nuevo; el test `Endpoints_SinSesion_Devuelven401_SalvoListaBlanca` incluye los 7 endpoints |
| T | CSRF que sube una planilla o modifica precios | Antiforgery global en POST y PUT, validado antes de leer el multipart; cookie `SameSite=Strict` |
| R | Cambios de precios sin rastro | Eventos con fecha, acción y cantidades. Sin atribución a una persona: AR-03 vigente |
| I | El 400 de formato, o el 500, devuelve o loguea el contenido de la planilla | Mensaje fijo de formato; títulos fijos; `ManejadorErrores` no loguea el mensaje de `FormatoPlanillaNoEsperado` |
| D | Multipart grande o lento que agota la memoria | `LimiteCuerpo` de 2 MB por ruta y 4 KB en el resto; un único archivo; `MultipartReader` con tope |
| E | Mass assignment de `Codigo` o `Activo` | Los DTO de entrada no tienen esos campos; JSON estricto |

### C3 — Casos de uso y dominio
| STRIDE | Amenaza | Mitigación |
|---|---|---|
| S | — | No aplica: no hay identidad en este componente |
| T | Valores fuera de rango o con decimales de más que desbordan el conversor ×10.000 (500) o generan precios absurdos | Topes de FR-23 y FR-24 y máximo de 2 decimales en el dominio, más CHECK en la base |
| R | — | Cubierto en C2 |
| I | — | No aplica |
| D | El recálculo masivo o la importación de 10.000 filas retienen el lock de escritura y otros pedidos reciben 503 | La planilla se parsea fuera de la transacción; cruce en memoria; `GuardarLote` cada 500; el recálculo solo corre si cambian alícuota, condición o múltiplo; el tiempo se mide (AC-25) |
| E | Que `RecalculoPrecios` se invoque fuera de una transacción y deje el catálogo a medio actualizar | Es un servicio de dominio y no un caso de uso registrado; la frontera transaccional queda en `ActualizarConfiguracion` |

### C4 — Lector de planillas (TB2)
| STRIDE | Amenaza | Mitigación |
|---|---|---|
| S | Un `.xlsm`, un `.csv` u otro tipo con extensión `.xlsx` | Se valida la firma ZIP, el content type del libro y que no haya `xl/vbaProject.bin` |
| T | Fórmulas que muestran un valor distinto al guardado | No se evalúan fórmulas: solo cuentan las celdas numéricas con valor; el resto es "precio inválido" |
| R | — | No aplica |
| I | XXE: un XML con DTD que lee archivos locales | OpenXml con `DtdProcessing.Prohibit`; test `Lector_XmlConDtd_Rechaza` |
| D | **ZIP bomb**, XML gigante o demasiadas filas | `InspectorZip`: hasta 100 entradas, 20 MB descomprimidos en total y 10 MB por entrada, relación de compresión hasta 100:1; lectura SAX; hasta 10.000 filas |
| E | Ejecución de macros o de contenido activo | No se ejecuta nada; se rechaza `vbaProject.bin` |

### C5 — Persistencia
| STRIDE | Amenaza | Mitigación |
|---|---|---|
| S | — | No aplica |
| T | Inyección SQL por el texto de búsqueda | `LIKE` parametrizado con escape de `\`, `%` y `_`; test `Repositorio_Buscar_PorcentajeYGuionBajo_SeEscapan` |
| R | — | No aplica |
| I | Otro usuario local lee costos y márgenes del archivo | ACL de `%ProgramData%\OpticaSistema` (FEAT-001a); sin cifrado: **AR-05** |
| D | Búsqueda sin límite que devuelve 10.000 filas | Hasta 200 resultados, con la marca `truncado` |
| E | — | No aplica |

### C6 — Logs (TB4)
| STRIDE | Amenaza | Mitigación |
|---|---|---|
| S | — | No aplica |
| T | — | No aplica |
| R | Importaciones sin rastro | Evento `PlanillaImportada` con cantidades |
| I | Costos, códigos o celdas en el Visor de eventos o en el log Trace de MVC | Eventos solo con cantidades y el código interno; los DTO son clases sin `ToString` revelador; el parámetro de búsqueda está en una lista blanca justificada; test `Logs_NoContienenCostosNiCodigos` |
| D | — | No aplica |
| E | — | No aplica |

## 5. Cadena de suministro (W-TM-01)

**Paquete nuevo:** `DocumentFormat.OpenXml`.
- Lo publica Microsoft, con licencia MIT, y no trae dependencias de terceros fuera de las del runtime.
- Se fija con versión exacta en los lockfiles.
- Antes del cierre de CODE se audita con `dotnet list package --vulnerable --include-transitive`.

En `e2e/` no se agregan dependencias: las planillas de prueba se generan con la API de zip de Node.

La elección queda registrada en el ADR-002.

## 6. Disponibilidad (W-TM-02)

**Escenario de uso:** 1 PC y 2 operadoras.

**Vectores:**
- Lock de escritura durante la importación o el recálculo. Está acotado por la lectura fuera de la transacción y el guardado por lotes; un escritor simultáneo puede recibir 503 si el lock pasa de 5 s.
- Planillas maliciosas. Están acotadas por los límites de C4.

## 7. Riesgos clasificados

| Id | Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|---|---|---|---|---|---|
| R-01 | ZIP bomb o XML gigante en la planilla | D | Media | **Alto** | `InspectorZip` más SAX más límite de 2 MB (spec Block 4) |
| R-02 | XXE en el XML de la planilla | I | Baja | **Alto** | `DtdProcessing.Prohibit` más test (Block 4) |
| R-03 | La planilla del proveedor queda en `%TEMP%` (volcado de `IFormFile` a partir de 64 KB) | I | **Alta** sin mitigar | **Alto** | `MultipartReader` a `MemoryStream`, sin `IFormFile`, más el test `Planilla_NoCreaArchivosTemporales` (Block 5) |
| R-04 | Costos o contenido de celdas en los logs | I | Media | **Alto** | Eventos solo con cantidades, mensajes fijos y test de logs (Blocks 4 y 5) |
| R-05 | Costos y márgenes en la caché del navegador | I | Media | Medio | `Cache-Control: no-store` (Block 5) |
| R-06 | CSRF en la carga de la planilla o en la edición de precios | T | Baja | Alto | Antiforgery global más `SameSite=Strict` (FEAT-001a) y test `Planilla_SinAntiforgery_Devuelve400` |
| R-07 | Inyección SQL o comodines en la búsqueda | T | Baja | Medio | `LIKE` parametrizado y escapado (Block 2) |
| R-08 | Desborde ×10.000 o decimales de más, que darían 500 o abortarían la importación | T/D | Media | Medio | Topes y 2 decimales en el dominio; "precio inválido" en la planilla (Blocks 1 y 4) |
| R-09 | Catálogo a medio actualizar si falla el recálculo | T | Baja | Alto | Una sola transacción por `ActualizarConfiguracion`; test `ActualizarConfiguracion_FallaGuardarArticulos_RevierteConfiguracion` |
| R-10 | Macros o archivos que no son `.xlsx` | E/S | Baja | Medio | Firma ZIP, content type y rechazo de `vbaProject.bin` |
| R-11 | Lectura de `optica.db` por otro usuario local | I | Baja | Medio | ACL; sin cifrado: **AR-05** |
| R-12 | Cruce de la planilla que actualiza el artículo equivocado ("AÑO-1" = "ANO-1") | T | Baja | Medio | La clave respeta los acentos (FR-13, decisión del usuario); test `ImportarCostos_CodigoConAcentoDistinto_NoCoincide` |
| R-13 | Lock prolongado que provoca 503 en otros pedidos | D | Baja | Bajo | Parseo fuera de la transacción, guardado por lotes y medición (AC-25) |

**Resumen:** 0 críticos, 6 altos (R-01, R-02, R-03, R-04, R-06, R-09), 5 medios (R-05, R-07, R-08, R-10, R-12) y 2 bajos (R-11 y R-13 residuales). Todos los altos tienen la mitigación incorporada en la spec.

## 8. Riesgos aceptados (F-TM-04)

| Id | Riesgo | Quién lo acepta | Justificación | Condición de revisión |
|---|---|---|---|---|
| AR-01 a AR-04 | Heredados de FEAT-001a | Usuario (2026-10-16) | Ver `threat-FEAT-001a.md` | Ver `threat-FEAT-001a.md` |
| AR-05 | Costos y márgenes de proveedores en `optica.db` sin cifrar en reposo | Usuario (Diego), 2026-10-16 | Es una sola PC con la ACL restringida de FEAT-001a; los datos son comerciales y no personales; el cifrado en reposo de la base ya está planificado para FEAT-001c, que guarda datos personales | Se revisa en el threat model de FEAT-001c (antes de guardar datos personales), o si la base pasa a estar en una PC compartida o en la red |

## 9. Mitigaciones a incorporar en la spec

Ya figuran en `spec-FEAT-001b.md`:

1. `InspectorZip` con límites de entradas, tamaño descomprimido y relación de compresión, más el rechazo de `vbaProject.bin` y la validación del content type (Block 4).
2. Lectura SAX sin evaluar fórmulas, con hasta 10.000 filas y `DtdProcessing.Prohibit` (Block 4).
3. Excepciones del lector con mensaje fijo, sin contenido de celdas (Block 4, E4-1 y E4-2).
4. `MultipartReader` a `MemoryStream` de hasta 2 MB, sin `IFormFile`, y test de que no se crean temporales (Block 5).
5. `LimiteCuerpo` por ruta: 2 MB para la planilla y 4 KB para el resto (Block 5).
6. `Cache-Control: no-store` en `/api/*` (Block 5).
7. Eventos de log solo con cantidades; DTO como clases selladas; búsqueda en la lista blanca justificada; test `Logs_NoContienenCostosNiCodigos` (Block 5).
8. Topes de costo y margen, máximo de 2 decimales y CHECK en la base (Blocks 1 y 2).
9. `LIKE` parametrizado y escapado, con hasta 200 resultados (Blocks 2 y 3).
10. Recálculo como servicio de dominio dentro de la transacción de `ActualizarConfiguracion`, guardado por lotes (Blocks 1 y 3).
11. Clave de cruce que respeta los acentos (Block 1).
12. Paquete con versión exacta, lockfiles, auditoría de vulnerabilidades y ADR-002 (Block 4).
