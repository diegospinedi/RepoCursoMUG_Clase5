# Threat model — FEAT-001a: Acceso con contraseña y configuración del negocio

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001a |
| Fecha | 2026-10-16 |
| PRD | `docs/daw/prd/prd-FEAT-001a.md` |
| Spec | `docs/daw/specs/spec-FEAT-001a.md` |
| Método | STRIDE por componente |
| Resultado | PASSED (sin riesgos CRITICAL/HIGH sin mitigar) |

## 1. Arquitectura analizada

Despliegue: una PC del local con Windows. `Optica.Api` (ASP.NET Core, Kestrel) corre como servicio de Windows y **escucha solo en `127.0.0.1`** (decisión de la usuaria, 2026-10-16: las dos operadoras usan la misma PC). El SPA React se sirve desde `wwwroot` del mismo proceso.

| ID | Componente | Archivos principales |
|----|-----------|----------------------|
| C1 | SPA (navegador) | `web/src/paginas/{Ingreso,CrearContrasena,Configuracion}.tsx`, `web/src/api/cliente.ts`, `web/src/App.tsx` |
| C2 | API HTTP | `src/Optica.Api/Program.cs`, `Controllers/AccesoController.cs`, `Controllers/ConfiguracionController.cs`, `Errores/ManejadorErrores.cs` |
| C3 | Casos de uso y dominio | `src/Optica.Application/Acceso/*`, `Configuracion/*`; `src/Optica.Domain/Acceso/*`, `Configuracion/*` |
| C4 | Persistencia SQLite | `src/Optica.Infrastructure/Persistencia/*` → archivo `optica.db` (+ `-wal`, `-shm`) en `Optica:RutaBase` (por defecto `%ProgramData%\OpticaSistema\`) |
| C5 | Comando de blanqueo | `src/Optica.Api/Herramientas/ComandoBlanqueo.cs` → caso de uso `BlanquearContrasena` |
| C6 | Claves de Data Protection | directorio configurable fuera del repo, protegido con DPAPI |
| C7 | Logs | logging de ASP.NET Core + log de auditoría local del blanqueo |

Endpoints:

| Método | Ruta | Acceso |
|--------|------|--------|
| GET | `/api/acceso/ingreso` | anónimo (devuelve solo `contrasenaDefinida`, `bloqueadoHasta`, `sesionActiva`; emite token antiforgery) |
| POST | `/api/acceso/contrasena-inicial` | anónimo + antiforgery; 409 si ya existe |
| POST | `/api/acceso/ingreso` | anónimo + antiforgery; 401 / 423 |
| POST | `/api/acceso/salida` | sesión + antiforgery |
| PUT | `/api/acceso/contrasena` | sesión + antiforgery |
| GET / PUT | `/api/configuracion` | sesión (+ antiforgery en PUT) |
| GET | `/*` fallback SPA y assets | anónimo (sin datos) |

## 2. Fronteras de confianza (F-TM-02)

| ID | Frontera | Cruce |
|----|----------|-------|
| TB1 | Navegador ↔ API | HTTP sobre la interfaz loopback `127.0.0.1` |
| TB2 | Otros procesos y sitios web abiertos en la misma PC ↔ puerto loopback | cualquier proceso local o una página maliciosa (DNS rebinding, CSRF) puede enviar pedidos a `127.0.0.1` |
| TB3 | Proceso de la API ↔ sistema de archivos | `optica.db`, claves de Data Protection, logs |
| TB4 | Usuario de Windows en consola ↔ comando de blanqueo ↔ base | ejecución local con argumento de línea de comandos |
| TB5 | Registros npm/NuGet ↔ build | dependencias de terceros |

## 3. Clasificación de datos (F-TM-05) y cifrado (F-TM-07)

| Dato | Clase | En tránsito | En reposo |
|------|-------|-------------|-----------|
| Contraseña en texto plano | Credencial | Solo por loopback (TB1); no atraviesa ninguna red. Sin TLS: riesgo aceptado AR-01. | Nunca se guarda. Solo en memoria durante el pedido; no se loguea. |
| Hash + sal + `SelloSeguridad` | Credencial | No sale del servidor. | PBKDF2-HMAC-SHA256, sal aleatoria de 16 bytes, 600.000 iteraciones. Archivo de base sin cifrar con ACL restringida: riesgo aceptado AR-02. |
| Cookie de sesión | Credencial | Loopback; `HttpOnly`, `SameSite=Strict`. | Cifrada y firmada por Data Protection (AES-256-CBC + HMAC-SHA256); claves protegidas con DPAPI. |
| Claves de Data Protection | Credencial | No salen del servidor. | DPAPI (`ProtectKeysWithDpapi`) + ACL del directorio. |
| Configuración del negocio | Interno del negocio (no PII) | Loopback. | SQLite, enteros escalados (ADR-001). |
| Log de auditoría del blanqueo (fecha, hora, usuario de Windows) | Interno; el usuario de Windows es dato personal mínimo | No sale del servidor. | Archivo local con ACL restringida. |

> **Aviso para FEAT-001c:** desde ese sub-ticket la base guarda PII de clientes (Ley 25.326). F-TM-07 va a exigir definir el cifrado en reposo de esa información; este threat model no lo resuelve.

## 4. Análisis STRIDE por componente (F-TM-01)

### C1 — SPA
| STRIDE | Amenaza | Mitigación |
|--------|---------|------------|
| S | Página falsa que imita la pantalla de ingreso | Fuera del alcance técnico: el acceso es por `http://localhost`; documentado en `docs/operacion/instalacion.md` (acceso directo en el escritorio). |
| T | XSS que altere la UI o lea respuestas | React escapa por defecto; prohibido `dangerouslySetInnerHTML`; CSP `default-src 'self'; frame-ancestors 'none'` enviada por C2. |
| R | — | No aplica: la UI no registra acciones; ver C2. |
| I | Contraseña retenida en el estado del navegador o en `localStorage` | La contraseña solo vive en el estado del formulario y se descarta tras el envío; prohibido guardarla en storage. |
| D | — | No aplica (cliente local). |
| E | Mostrar pantallas de la app sin sesión | La guarda de ruteo solo es UX; la autorización real está en C2 (todo `/api` exige sesión). |

### C2 — API HTTP
| STRIDE | Amenaza | Mitigación |
|--------|---------|------------|
| S | Fuerza bruta contra `/ingreso` | Bloqueo de 5 min tras 5 fallos (NFR-02), persistido; caso de uso serializado (`SemaphoreSlim`) para que pedidos simultáneos no superen 5 intentos; mínimo 8 caracteres. |
| S | Reutilizar una cookie después de un cambio o blanqueo de contraseña | `SelloSeguridad` en el claim, validado en cada pedido (`OnValidatePrincipal` → `ValidarSesion`); se regenera al crear, cambiar y blanquear. |
| S | DNS rebinding: un sitio web malicioso abierto en la PC resuelve su dominio a `127.0.0.1` y llama a la API anónima (por ejemplo, `contrasena-inicial` antes de que se cree) | Filtrado de host: `AllowedHosts = localhost;127.0.0.1`; cualquier otro `Host` → 400. |
| T | CSRF sobre ingreso, creación inicial, cambio de contraseña o configuración | `SameSite=Strict` + token antiforgery obligatorio en todo POST/PUT, incluidos `ingreso` y `contrasena-inicial`; token regenerado tras el ingreso. |
| T | Clickjacking de la pantalla de Configuración | `X-Frame-Options: DENY` y `frame-ancestors 'none'`. |
| T | Mass assignment en `PUT /api/configuracion` | DTO explícito con los 4 campos; validación en dominio (FR-12 a FR-14). |
| R | Cambios de configuración o de contraseña sin rastro | Log de eventos (fecha, hora, acción, resultado) sin datos sensibles. Sin atribución a persona: riesgo aceptado AR-03. |
| I | Endpoint que quede sin autenticación | `FallbackPolicy` = usuario autenticado; lista blanca explícita de anónimos (tabla de endpoints); test de AC-01 que recorre `EndpointDataSource` y exige 401 fuera de la lista. |
| I | Contraseña en logs | Sin `HttpLogging` de cuerpos; los casos de uso no loguean la contraseña; test que la busca en la salida de logs. |
| I | Detalle de excepciones en las respuestas | `ProblemDetails` sin stack trace fuera de Development. |
| I | `GET /api/acceso/ingreso` revela información | Devuelve solo `contrasenaDefinida`, `bloqueadoHasta` y `sesionActiva`; ningún dato de negocio. |
| D | Bloqueo global usado para dejar afuera a las operadoras | Solo alcanzable desde la misma PC (loopback + filtrado de host); riesgo aceptado AR-04. |
| D | Costo de CPU de PBKDF2 por intento | Acotado por el bloqueo (máximo 5 intentos cada 5 min) y la serialización. |
| E | Pedido a `/api/*` sin sesión que termina en un redirect HTML en lugar de 401 | `OnRedirectToLogin`/`OnRedirectToAccessDenied` devuelven 401/403 para `/api/*`. |

### C3 — Casos de uso y dominio
| STRIDE | Amenaza | Mitigación |
|--------|---------|------------|
| S | Dos creaciones iniciales simultáneas que dejan dos contraseñas | Fila única `Id = 1` con `CHECK (Id = 1)`; creación = `UPDATE … WHERE Hash IS NULL`; 0 filas → 409. |
| T | Saltear la validación de longitud o de rangos | Value object `ContrasenaNueva` y validaciones en las entidades de Domain, independientes de la interfaz que invoque. |
| R | — | Cubierto en C2 y C5. |
| I | — | El dominio no expone el hash fuera de Infrastructure. |
| D | — | No aplica. |
| E | Cambiar la contraseña sin conocer la actual | `CambiarContrasena` exige sesión y verifica la contraseña actual (AC-06). |

### C4 — Persistencia SQLite
| STRIDE | Amenaza | Mitigación |
|--------|---------|------------|
| S | — | No aplica. |
| T | Inyección SQL | EF Core con consultas parametrizadas; el único SQL explícito (`UPDATE` condicional) usa parámetros. |
| T | Otro usuario de Windows edita el archivo de la base | ACL de `%ProgramData%\OpticaSistema\`: solo la cuenta del servicio y Administradores. |
| R | — | No aplica. |
| I | Lectura del archivo para atacar el hash fuera de línea | PBKDF2 con 600.000 iteraciones + ACL; riesgo residual aceptado AR-02. |
| I | Base creada dentro del repo y commiteada | El servidor no arranca si `Optica:RutaBase` cae dentro del árbol de fuentes; `.gitignore` con `*.db`, `*.db-wal`, `*.db-shm`. |
| D | Base bloqueada o corrupta | WAL + `busy_timeout`; backup en FEAT-001e. |
| E | — | No aplica. |

### C5 — Comando de blanqueo
| STRIDE | Amenaza | Mitigación |
|--------|---------|------------|
| S | Blanqueo disparado sin intención por configuración o variable de entorno | Se detecta solo en los `args` crudos, antes de construir el host; nunca desde `IConfiguration`; test "arrancar sin el flag nunca blanquea". |
| T | Blanqueo remoto | No existe endpoint web (AC-08); no levanta Kestrel. |
| R | Blanqueo sin rastro | Log de auditoría local con fecha, hora y usuario de Windows. |
| I | — | No muestra el hash ni datos. |
| D | Blanqueo como forma de dejar afuera a las operadoras | Requiere acceso local con permisos de escritura sobre la base (ACL: cuenta del servicio y Administradores). |
| E | Un usuario de Windows sin privilegios blanquea y toma el control | La ACL de la base impide escribirla; el comando falla con código de salida ≠ 0. |

### C6 — Claves de Data Protection
| STRIDE | Amenaza | Mitigación |
|--------|---------|------------|
| S/E | Robo de claves para fabricar cookies válidas | `ProtectKeysWithDpapi` + ACL del directorio + validación del `SelloSeguridad` en cada pedido. |
| T | Reemplazo de claves | ACL del directorio. |
| R | — | No aplica. |
| I | Claves efímeras que invalidan sesiones en cada reinicio | `PersistKeysToFileSystem` en ruta configurable fuera del repo. |
| D | Pérdida del directorio de claves | Solo invalida sesiones abiertas: se vuelve a ingresar. |

### C7 — Logs
| STRIDE | Amenaza | Mitigación |
|--------|---------|------------|
| T | Alteración del log de auditoría | ACL restringida del directorio de logs. |
| R | — | El log existe para cubrir repudio (C2, C5). |
| I | Contraseña o cookie en los logs | Sin logging de cuerpos ni de headers `Cookie`; test de logs. |
| S/D/E | — | No aplica. |

## 5. Cadena de suministro (W-TM-01)
Dependencias nuevas: paquetes NuGet de EF Core SQLite y de testing; npm de React, Vite, Vitest, Testing Library y Playwright. Mitigación: versiones fijadas con lockfiles (`packages.lock.json` con `RestorePackagesWithLockFile`, `package-lock.json`), sin rangos flotantes, y `dotnet list package --vulnerable` más `npm audit --omit=dev` en el SAST de CODE. Ninguna dependencia nueva se ejecuta fuera del build o del servidor local.

## 6. Disponibilidad (W-TM-02)
Servicio interno con 1 PC y 2 operadoras. Vectores: bloqueo global (AR-04) y costo de PBKDF2 (acotado por el bloqueo). Sin otros vectores relevantes: no hay exposición en red.

## 7. Riesgos clasificados

| ID | Riesgo | STRIDE | Probabilidad | Impacto | Mitigación |
|----|--------|--------|--------------|---------|------------|
| R-01 | Fuerza bruta de la contraseña | S | Media | Alto | Bloqueo persistido 5/5 + serialización + mínimo 8 |
| R-02 | DNS rebinding contra la API anónima | S/T | Baja | Alto | Filtrado de host |
| R-03 | Sesión vigente tras cambio o blanqueo | S | Baja | Alto | `SelloSeguridad` validado por pedido |
| R-04 | Endpoint expuesto sin sesión | I/E | Media | Alto | `FallbackPolicy` + test por `EndpointDataSource` |
| R-05 | CSRF sobre acciones de acceso o configuración | T | Baja | Medio | SameSite=Strict + antiforgery en todo POST/PUT |
| R-06 | Doble creación inicial concurrente | T | Baja | Alto | Fila única + `UPDATE` condicional |
| R-07 | Contraseña en logs | I | Media | Alto | Sin logging de cuerpos + test |
| R-08 | Blanqueo disparado por configuración | T | Baja | Alto | Flag solo por `args` crudos + test |
| R-09 | Lectura del archivo de base por otro usuario local | I | Baja | Medio | ACL + PBKDF2 (residual AR-02) |
| R-10 | Robo de claves de Data Protection | S/E | Baja | Alto | DPAPI + ACL + sello |
| R-11 | Clickjacking / XSS | T | Baja | Medio | Headers de seguridad + CSP + React |
| R-12 | Base commiteada al repo | I | Media | Alto | Chequeo al arrancar + `.gitignore` |
| R-13 | Dependencia vulnerable | T/E | Baja | Medio | Lockfiles + auditoría en SAST |
| R-14 | Bloqueo global que deja afuera a las operadoras | D | Baja | Bajo | Loopback (residual AR-04) |
| R-15 | Acciones sin atribución a persona | R | Media | Bajo | Log de eventos (residual AR-03) |

Totales: CRITICAL 0 · HIGH 0 sin mitigar (8 de impacto alto, todos mitigados) · MEDIUM 4 · LOW 3.

## 8. Riesgos aceptados (F-TM-04)

| ID | Riesgo | Quién lo aceptó | Justificación | Condiciones de revisión |
|----|--------|-----------------|---------------|-------------------------|
| AR-01 | La contraseña y la cookie viajan sin TLS | Responsable del proyecto, 2026-10-16 (elección de la opción "solo localhost" en PLAN) | El tráfico usa solo la interfaz loopback y nunca sale de la PC; TLS no agrega protección frente a un atacante que ya controla la PC | Si el sistema pasa a usarse desde otra PC o a escuchar en otra interfaz |
| AR-02 | El archivo de base no está cifrado; el hash de la contraseña queda expuesto a quien tenga acceso de lectura | Responsable del proyecto, 2026-10-16; confirmado al aprobar PLAN el 2026-10-16 | El hash PBKDF2 de 600.000 iteraciones es la protección de la credencial; la ACL limita la lectura a la cuenta del servicio y Administradores | En el threat model de FEAT-001c, cuando la base empiece a guardar PII de clientes |
| AR-03 | Las acciones no se atribuyen a una persona (contraseña compartida) | Responsable del proyecto, 2026-10-16 (PRD aprobado: módulo de usuarios fuera de alcance; confirmado al aprobar PLAN el 2026-10-16) | No hay módulo de usuarios; el log registra fecha, hora y acción | Cuando se incorpore el módulo de usuarios o ante un cambio de configuración en disputa |
| AR-04 | Un proceso de la misma PC puede provocar el bloqueo global de 5 minutos | Responsable del proyecto, 2026-10-16; confirmado al aprobar PLAN el 2026-10-16 | El bloqueo es temporal y se libera solo (NFR-02); solo alcanzable desde la misma PC | Si se observan bloqueos no explicados o el sistema se expone en red |

## 9. Mitigaciones a incorporar en la spec
1. Kestrel solo en `127.0.0.1` y `AllowedHosts = localhost;127.0.0.1`.
2. `FallbackPolicy` autenticada + lista blanca de anónimos + test por `EndpointDataSource`.
3. `SelloSeguridad` regenerado en crear/cambiar/blanquear y validado en `OnValidatePrincipal`.
4. Antiforgery en todo POST/PUT, emitido con `GET /api/acceso/ingreso` y regenerado tras el ingreso.
5. Bloqueo persistido y caso de uso de ingreso serializado.
6. Fila única `Id = 1` + `UPDATE` condicional en la creación inicial.
7. Blanqueo solo por `args` crudos, sin Kestrel, vía caso de uso, con log de auditoría.
8. Sin logging de cuerpos ni cookies; test de logs; `ProblemDetails` sin stack trace.
9. Headers `X-Frame-Options: DENY`, `X-Content-Type-Options: nosniff`, CSP `default-src 'self'; frame-ancestors 'none'`.
10. Data Protection persistida con DPAPI; ACL de `%ProgramData%\OpticaSistema\` documentada en la instalación.
11. Chequeo al arrancar: la base no puede estar dentro del árbol de fuentes; `.gitignore` con archivos de base.
12. Lockfiles de NuGet y npm; auditoría de dependencias en el SAST.
