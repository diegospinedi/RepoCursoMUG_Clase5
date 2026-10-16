# SAST — FEAT-001a: Acceso con contraseña y configuración del negocio

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001a |
| Fecha | 2026-10-16 |
| Rama | `feat/FEAT-001a-acceso-configuracion` (contra `main`) |
| Alcance | `src/` (Domain, Application, Infrastructure, Api), `web/src`, `e2e/` |
| Resultado | **PASSED** — 0 Critical, 0 High, 0 Medium abiertos |

## Resultado por categoría

| Regla | Qué se revisó | Resultado |
|---|---|---|
| F-SAST-01 Secretos | Patrones de clave, contraseña, token y cadena de conexión en `src`, `web/src` y `e2e`; archivos sensibles versionados (`.env`, `.pfx`, `.p12`, `.pem`, `.db`, `key-*.xml`, `*.log`); `.gitignore` | ✅ Sin secretos. `.gitignore` cubre `.env*`, `*.pfx`, `*.db*`, `*.log`, `key-*.xml` |
| F-SAST-02 Inyección SQL | `FromSqlRaw`, `ExecuteSqlRaw`, `SqlQueryRaw`, `CommandText` | ✅ Solo `InterceptorConexionSqlite.cs:22,32` con PRAGMAs constantes; `CrearSiNoExiste` usa `ExecuteUpdateAsync` parametrizado |
| F-SAST-03 Inyección de comandos | `Process.Start`, `child_process`, `shell: true` | ✅ Sin uso en producción. `e2e/scripts` usa `spawn`/`spawnSync` sin shell y con argumentos validados |
| F-SAST-04 / F-SAST-17 Funciones inseguras | `eval`, `new Function`, deserialización insegura | ✅ Sin uso. JSON estricto (`UnmappedMemberHandling = Disallow`) |
| F-SAST-05 Path traversal | `File.*`, `Path.Combine` | ✅ Las rutas vienen de configuración validada (absolutas y fuera del repo, `OpcionesOptica`), nunca de la entrada del usuario |
| F-SAST-06 XSS | `dangerouslySetInnerHTML`, `innerHTML` | ✅ Sin uso en `web/src`. CSP `default-src 'self'` sin `unsafe-inline` |
| F-SAST-07 SSRF | Pedidos salientes construidos con datos del usuario | ✅ No hay pedidos salientes en este ticket |
| F-SAST-08 Criptografía débil | MD5, SHA1, DES, ECB, `Random` | ✅ PBKDF2-HMAC-SHA256, 600.000 iteraciones, sal de 16 bytes con `RandomNumberGenerator`, `FixedTimeEquals` |
| F-SAST-09 Debug en producción | `UseDeveloperExceptionPage`, `EnableDetailedErrors` | ✅ Sin uso. El 500 no expone detalle en ningún entorno |
| F-SAST-10 Datos sensibles en logs | `EnableSensitiveDataLogging`, `HttpLogging`, logs con contraseña, hash o sello | ✅ Sin uso. DTOs con secretos como clases sin `ToString` revelador; tests `Logs_NoContienenLaContrasena` y `Dtos_ToStringNoMuestraTextos` |
| F-SAST-11 Upload | Carga de archivos | ✅ No aplica |
| F-SAST-12 CSRF | Antiforgery en todo POST/PUT | ✅ `FiltroAntiforgery` global con `X-XSRF-TOKEN`, cookie de sesión `SameSite=Strict`. Los `[AllowAnonymous]` (`AccesoController.cs:47,61,83`) son la lista blanca de la spec y también exigen antiforgery |
| F-SAST-13 / F-SAST-16 Dependencias | `dotnet list package --vulnerable --include-transitive`, `npm audit` en `web/` y `e2e/` | ✅ 0 vulnerabilidades en los 8 proyectos .NET y en los dos paquetes npm |
| F-SAST-14 Validación de entrada | Límite de cuerpo, largo de contraseñas, reglas de dominio | ✅ 4 KB por pedido, 128 caracteres antes de PBKDF2, validación de dominio y de host |
| F-SAST-15 Errores que filtran detalles internos | `ex.Message`, `StackTrace` en respuestas | ✅ `ManejadorErrores` responde con títulos fijos |

## Hallazgos Low / Informational (no bloquean)

| # | Ubicación | Hallazgo | Disposición |
|---|---|---|---|
| L-1 | `e2e/tests/soporte.ts:14-16` | Contraseñas de prueba escritas en el código | Falso positivo: son ficticias y solo se usan contra bases temporales. La documentación prohíbe usar contraseñas reales en la suite |
| L-2 | `web/vite.config.ts` | `server.fs.allow: ['..']` expone la raíz del repo en el servidor de desarrollo | Mitigado con `fs.deny` ampliado (certificados, claves, bases, logs, secretos) y escucha solo en 127.0.0.1. Restringir `fs.allow` en un ciclo futuro de la spec |
| L-3 | `src/Optica.Application/Acceso/IniciarSesion.cs` | Los ingresos se serializan sin timeout ni cancelación: una ráfaga de pedidos demora el ingreso | Bajo riesgo: el servidor solo escucha en loopback. Pendiente para registrar en el threat model |
| L-4 | `src/Optica.Api/Herramientas/ComandoBlanqueo.cs` | La línea de auditoría se escribe después del blanqueo | Bajo riesgo: si el proceso se corta entre los dos pasos, el blanqueo queda sin línea de auditoría. Pendiente |

## Supresiones

Ninguna.
