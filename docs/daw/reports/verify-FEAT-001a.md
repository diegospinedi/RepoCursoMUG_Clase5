# Verificación — FEAT-001a: Acceso con contraseña y configuración del negocio

| Campo | Valor |
|-------|-------|
| Ticket | FEAT-001a |
| Tier | FEATURE |
| PRD | `docs/daw/prd/prd-FEAT-001a.md` |
| Spec | `docs/daw/specs/spec-FEAT-001a.md` |
| Threat model | `docs/daw/security/threat-FEAT-001a.md` |
| SAST | `docs/daw/security/sast-FEAT-001a.md` |

---

## Ronda 1 — 2026-10-16 — BLOCKED

Verificación cruzada por `daw-module-verifier`, un agente que no participó en la implementación.

### Ejecución

- `dotnet build Optica.sln` (`TreatWarningsAsErrors`): 0 warnings, 0 errores.
- `dotnet test Optica.sln` con coverlet: 133/133 (Domain 26, Application 23, Infrastructure 22, Api 62).
- `npm run test` en `web/`: 30/30.
- `npm run build` en `web/` (tsc estricto): sin errores.
- `npm run test:harness` en `e2e/`: 24/24.
- E2E oficial en Windows, 2026-10-16: 12/12 con Chrome 155.0.8059.80 y Edge 155.0.4283.45.

### Resultado por regla

| Regla | Resultado | Detalle |
|---|---|---|
| F-VER-01 | ❌ | AC-01 a AC-16: ✅. AC-17: ⚠️ (falta test de marca en la pantalla de creación de contraseña). AC-18: ❌ (solo la versión vigente de Chrome y Edge; falta la inmediata anterior que exige NFR-04) |
| F-VER-02 | ✅ | Los 6 bloques tienen todos sus archivos y tareas |
| F-VER-03 | ❌ | `web/src`: ramas 70,68 % (135/191), por debajo del 80 %. .NET: ✅ |
| F-VER-04 | ✅ | Los 7 endpoints, el comando de blanqueo, el dominio y los formularios tienen tests de camino triste |
| F-VER-05 | ✅ | Build .NET y tsc sin errores. ⚠️ No hay ESLint configurado |
| F-VER-06 | ✅ | Los 117 tests que nombra la spec existen y pasan. ⚠️ Las cantidades difieren de la spec por los tests agregados en las revisiones |
| W-VER-01 | ⚠️ | Dos `using` sin uso: `tests/Optica.Api.Tests/GuardaLoopbackTests.cs:3` y `tests/Optica.Infrastructure.Tests/BaseTemporal.cs:4` |
| W-VER-02 | ✅ | Dominio y aplicación por encima del 90 % |
| W-VER-03 | ⚠️ | Reloj real en `RepositoriosTests.cs:374,434` y `ComandoBlanqueoTests.cs:133-138`; `SemaphoreSlim` estático compartido entre fábricas de test |

### Cobertura (líneas / ramas / funciones)

| Proyecto | Líneas | Ramas | Funciones |
|---|---|---|---|
| Optica.Domain | 94,1 % | 95,4 % | 94,5 % |
| Optica.Application | 99,4 % | 93,3 % | 100 % |
| Optica.Infrastructure | 93,5 % | 94,2 % | 96,9 % |
| Optica.Api (sin código generado) | 94,2 % | 91,3 % | 88,7 % |
| web/src | 87,97 % | **70,68 %** | 94,2 % |

Exclusiones: migraciones generadas, `Program.cs`, `main.tsx`, archivos de test, `src/pruebas/**` y el regex generado (`[GeneratedRegex]`). Si se cuenta el regex generado, las ramas de Optica.Api bajan a 71,5 %.

La cobertura web se midió con `@vitest/coverage-v8@5.0.3` sobre una copia de `web/` fuera del repo, porque el proyecto no tiene la herramienta instalada.

**Ramas web sin cubrir:**
- `Ingreso.tsx:14,19,23,62-72`
- `CrearContrasena.tsx:51-58`
- `Layout.tsx:31-32`
- `App.tsx:44-72` (falla de `obtenerEstadoAcceso` y botón Reintentar)
- `Configuracion.tsx:76,270-287`
- `cliente.ts:82-109,180`

### Verificación final de la spec

- **Reglas de dependencia:** ✅. EF Core y SQLite solo están en Infrastructure.
- **Mitigaciones del threat model §9:** ✅ 12/12. La L-4 del SAST, línea de auditoría posterior al blanqueo, queda abierta como riesgo bajo.
- **`git status`:** ✅ sin `*.db*` ni claves.

### Evidencia TDD

En la verificación cruzada se informó como faltante. La evidencia existe en los reportes de cada implementador durante CODE, que fueron revisados por `daw-module-verifier` bloque por bloque:

| Bloque | Evidencia de rojo antes del verde |
|---|---|
| 1 | Ronda anterior a esta sesión. Commit `9592d16` |
| 2 | 16/16 fallaron por aserción contra un esqueleto ingenuo: hasher en texto plano, conversor sin escala, migración sin semillas, unidad sin transacción. Con `BEGIN` diferido falló solo `ConexionOcupada_EsperaBusyTimeout` |
| 3 | 21/21 fallaron contra stubs con `NotImplementedException`. Es evidencia débil, aceptada por el verificador del bloque tras revisar las aserciones. Los dos tests agregados en la revisión se validaron por mutación |
| 4 | 38/38 fallaron por aserción contra un esqueleto que compila. Los tests de las rondas 2 y 3 se validaron con rojo o mutación, test por test |
| 5 | 18/18 fallaron por aserción. Los tests de la ronda 2 se validaron por mutación |
| 6 | Una mutación de UI por test, más pruebas del harness por mutación. Sin código de producción nuevo, no hay "rojo antes" posible |

### FAILs que bloquean

1. **F-VER-03.** Ramas de `web/src` al 70,68 %. Hay que agregar los tests de error y de camino triste de las ramas listadas. Además, hay que dejar `@vitest/coverage-v8` en `devDependencies`, con versión exacta, y un script `test:coverage` con umbral del 80 %.
2. **F-VER-01 / AC-18.** Falta la corrida E2E con la versión anterior de Chrome y Edge (154).

### WARNINGs que no bloquean

- Las cantidades de tests difieren de la spec. Hay que actualizarlas en el próximo ciclo de spec.
- Configurar coverlet con `ExcludeByAttribute=GeneratedCodeAttribute`.
- AC-17 en la pantalla de creación de contraseña.
- No hay ESLint configurado.
- Hay dos `using` sin uso en tests.
- Tests con reloj real y estado estático compartido.

### Datos sensibles

- No hay datos reales de clientes, empleados, proveedores ni de la empresa en el repo.
- Las contraseñas de test son ficticias.
- **Alerta para FEAT-001c (Ley 25.326):** `optica.db` no está cifrado (AR-02). El cifrado en reposo tiene que resolverse en ese threat model, antes de guardar datos personales de clientes.

---

## Ronda 2 — 2026-10-16 — PASSED

Se hizo después del loop correctivo en CODE (commits `a3c4412` y `e3ba8ba`). La verificación cruzada estuvo a cargo de `daw-module-verifier`.

### Ejecución

- `npm run test:coverage` en `web/`: 68/68 con salida 0. Sentencias 97,81 %, ramas 92,14 % (176/191), funciones 100 %, líneas 97,78 %.
- `dotnet test Optica.sln --settings coverlet.runsettings`: 133/133.
- `npm run build` en `web/`: sin errores.
- `git diff 0a480f5..HEAD -- src/`: vacío. El loop no tocó código de producción.

### Evidencia de AC-18 (NFR-04)

| Navegador | Versión | Resultado | Fecha |
|---|---|---|---|
| Chrome (vigente) | 155.0.8059.80 | 6/6 | 2026-10-16 |
| Edge (vigente) | 155.0.4283.45 | 6/6 | 2026-10-16 |
| Chrome for Testing (anterior) | 154.0.8037.92 | 6/6 | 2026-10-16 |
| Edge (anterior) | 154 | No probado: riesgo aceptado | — |

- **Ejecución:** las tres corridas se hicieron desde un clon temporal del repo en Windows. La de Chrome 154 usó `OPTICA_E2E_EJECUTABLE_CHROME=%TEMP%\chrome-cft-154\chrome-win64\chrome.exe` con `npx playwright test --project=chrome`.
- **Binario de Chrome 154:** salió de `https://storage.googleapis.com/chrome-for-testing-public/154.0.8037.92/win64/chrome-win64.zip`, con SHA-256 del zip `B897EF3601C947AC0620C784556DEC719AC602B0159CE105927ACF645EE0F598`.
- **Riesgo aceptado (Edge 154):** lo aceptó el usuario, Diego, el 2026-10-16. Edge no tiene una versión portable oficial de una versión anterior. Comparte motor Chromium con Chrome, y tanto Chrome 154 como Edge 155 pasaron.
- **Mitigación:** repetir la suite en Edge cuando salga Edge 156, para que la 155 quede probada como versión anterior. La desviación respecto de NFR-04 se lleva al próximo ciclo de PRD.

### Resultado por regla

| Regla | Resultado |
|---|---|
| F-VER-01 | ✅ AC-01 a AC-17. ⚠️ AC-18 con riesgo aceptado (Edge 154) |
| F-VER-02 | ✅ |
| F-VER-03 | ✅ web con ramas al 92,14 % y umbral del 80 % en `test:coverage`. .NET por encima del 80 % en todos los proyectos |
| F-VER-04 | ✅ |
| F-VER-05 | ✅ (⚠️ sin ESLint) |
| F-VER-06 | ✅ (⚠️ las cantidades difieren de la spec) |
| W-VER-01 | ✅ |
| W-VER-02 | ✅ |
| W-VER-03 | ⚠️ |

**Cobertura .NET por líneas**, medida sin excluir `CompilerGeneratedAttribute`:

| Proyecto | Líneas |
|---|---|
| Domain | 94,1 % |
| Application | 99,4 % |
| Infrastructure | 93,6 % |
| Api | 92,7 % |

### WARNINGs (no bloquean)

1. **AC-18:** Edge 154 sin probar, con el riesgo aceptado por el usuario.
2. **`coverlet.runsettings`:** excluye `CompilerGeneratedAttribute`, y eso deja afuera los cuerpos de los métodos `async` y de las lambdas. Hay que dejar solo `GeneratedCodeAttribute` en el próximo ciclo.
3. **ESLint:** no está configurado.
4. **Cantidad de tests:** las cantidades difieren de la spec (hay 133 en .NET y 68 en web). Hay que actualizarlas en el próximo ciclo de spec.
5. **Tests con reloj real y estado compartido:** `ComandoBlanqueoTests.cs:133,138` y `RepositoriosTests.cs:374,434` usan el reloj real, y `IniciarSesion.cs:18` tiene un `SemaphoreSlim` estático compartido entre tests.

### Veredicto

**PASSED**, con 0 FAILs y 5 WARNINGs.

### Datos sensibles

No hay datos reales en el diff. Sigue abierta la alerta para FEAT-001c: `optica.db` sin cifrar (AR-02, Ley 25.326).
