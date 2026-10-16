# Cobertura de pruebas

Cómo medir la cobertura del backend (.NET) y de la web (`web/`). El mínimo que se verifica en VERIFY es **80 %**
(regla F-VER-03 del pipeline).

## Web (`web/`)

Desde `web/`:

```bash
npm ci
npm run test:coverage
```

- Usa `vitest run --coverage` con `@vitest/coverage-v8` (versión exacta en `package.json`).
- La configuración está en `vite.config.ts`, en `test.coverage`:
  - mide `src/**/*.{ts,tsx}`;
  - excluye los tests (`**/*.test.*`), las ayudas de prueba (`src/pruebas/**`) y el punto de entrada `src/main.tsx`;
  - exige **80 %** en líneas, ramas, funciones y sentencias. Si alguna métrica queda abajo, el comando falla
    (código de salida distinto de 0) y lo dice al final: `ERROR: Coverage for branches (…) does not meet global threshold (80%)`.
- Informes: la tabla en la consola (`text`) y `web/coverage/coverage-summary.json` (`json-summary`), con los
  números por archivo. El reporter `text` omite los archivos con 100 % en todo; el JSON los incluye.
- `web/coverage/` no se commitea (está en `.gitignore`).

## .NET (`Optica.sln`)

Desde la raíz del repo:

```bash
dotnet test Optica.sln --settings coverlet.runsettings
```

- Usa `coverlet.collector`, que ya está en los cuatro proyectos de test.
- El criterio está en `coverlet.runsettings`:
  - `ExcludeByAttribute`: `GeneratedCodeAttribute` y `CompilerGeneratedAttribute`. Deja afuera el código que
    escriben los generadores, por ejemplo el de `[GeneratedRegex]`;
  - `ExcludeByFile`: `**/Persistencia/Migrations/*.cs`, las migraciones de EF Core que genera `dotnet ef`.
- Cada proyecto de test deja `coverage.cobertura.xml` y `coverage.json` en `tests/<proyecto>/TestResults/<guid>/`
  (ignorado por git). Cada informe mide solo lo que ejecutó **ese** proyecto de test. Por ejemplo,
  `RegistroAplicacion` lo cubren los tests de la API, no los de Application.

### Vista unificada (opcional)

Para ver un solo número por ensamblado, combinando los cuatro informes, se puede usar ReportGenerator sin
instalarlo (`dotnet dnx`, .NET 10):

```bash
dotnet dnx dotnet-reportgenerator-globaltool@5.5.11 --yes -- \
  "-reports:tests/*/TestResults/*/coverage.cobertura.xml" \
  "-targetdir:<directorio fuera del repo>" \
  "-reporttypes:TextSummary"
```

El resumen queda en `<directorio>/Summary.txt`. Antes de medir de nuevo, conviene borrar los `TestResults/`
anteriores, porque el patrón `*/coverage.cobertura.xml` toma todas las corridas.
