# Pruebas de punta a punta (Chrome y Edge)

Suite de Playwright de `e2e/` (NFR-04, AC-18). Recorre los flujos del PRD en el **Chrome y el Edge instalados en
la PC**, contra la API publicada con el SPA:

| Archivo | Tests (en este orden) |
|---|---|
| `e2e/tests/acceso.spec.ts` | `creación de contraseña e ingreso`, `sin sesión redirige a ingreso`, `cambio de contraseña`, `vencimiento redirige a ingreso` |
| `e2e/tests/configuracion.spec.ts` | `marca visible en ingreso y configuración`, `configuración: graba y muestra los 4 valores; múltiplo 0,001 muestra error` |

Cada test se corre en los proyectos `chrome` y `msedge`: 12 resultados en total. Además de sus aserciones, todo
test falla si el navegador registra una violación de CSP, una excepción no capturada o un error de consola que
el test no haya declarado. Un error esperado se declara con estado **y** ruta, por ejemplo el 401 de
`/api/acceso/ingreso` al ingresar con la contraseña anterior.

> **La suite NO se corre en la PC productiva de la óptica.** Solo en una PC de desarrollo o de prueba. Levanta
> servidores propios y publica la aplicación. Además, en la PC del local no tiene sentido detener el servicio real
> para probar.

## Cómo funciona

- `npx playwright test` elige una raíz nueva `optica-e2e-XXXXXX` en el temporal del sistema (`%TEMP%` en Windows,
  `/tmp` en Linux) y lanza `e2e/scripts/servidor-e2e.mjs`, el `webServer` de `e2e/playwright.config.ts`. Ese
  script:
  1. crea la raíz, que no puede existir de antes, y deja en ella un marcador del harness (`harness-e2e.json`);
  2. publica ahí la API con el SPA. Para eso usa `dotnet publish`, que corre `npm ci` y `npm run build` en `web/`;
  3. levanta una instancia por navegador en **puertos exclusivos de E2E**: `chrome` en `127.0.0.1:5180`, `msedge` en
     `127.0.0.1:5181` (y el auxiliar `chromium` en `5182`). El script rechaza cualquier otro puerto, incluido el
     **5080 del sistema real**. Cada instancia arranca con `ASPNETCORE_ENVIRONMENT=E2E` y tiene su propia base, sus
     claves y su auditoría dentro de la raíz. Esas tres rutas se pasan **por línea de comandos**
     (`--Optica:RutaBase=…`, `--Optica:DirectorioClaves=…`, `--Optica:DirectorioAuditoria=…`), que tiene prioridad
     sobre cualquier `appsettings*.json` y variable de entorno: aunque faltara `appsettings.E2E.json`, la API no
     puede caer en las rutas de `appsettings.json` (`%ProgramData%\OpticaSistema`);
  4. recién cuando **todas** las instancias pasaron la verificación de identidad (ver abajo), escribe la línea
     `OPTICA-E2E-LISTO <token>`. El token es aleatorio por corrida. Esa línea es lo único que espera Playwright
     (`webServer.wait`): no sondea ninguna URL, así que un servidor ajeno no puede dar la suite por lista.
- **Nunca se usa una instancia ajena.** El script controla todos los puertos antes de publicar y otra vez justo
  antes de arrancar cada instancia; si alguno está ocupado, la corrida se corta. Después del arranque comprueba
  que quien responde es la instancia propia:
  - el proceso hijo escribió exactamente `Now listening on: http://127.0.0.1:<puerto>`;
  - la base responde vacía;
  - la base recién creada existe en su directorio de datos;
  - un momento después, el proceso sigue vivo.

  Si algo de eso falla, la corrida se corta.
- `src/Optica.Api/appsettings.E2E.json` cambia dos cosas:
  - las tres rutas de datos, a partir de la variable `OPTICA_E2E_DATOS`. Es una segunda barrera: las rutas que
    valen son las de la línea de comandos. El harness exige que `OPTICA_E2E_DATOS` sea un hijo directo de la raíz
    de **esta** corrida. Si alguien usara el entorno `E2E` sin la variable ni los argumentos, la API **no
    arranca**, porque la ruta no es absoluta;
  - el registro de eventos de Windows (EventLog), que se apaga en todas sus categorías. **Con esto el archivo se
    aparta de "solo rutas"**, lo que pedía la spec. El motivo: en Windows, ASP.NET Core escribe por defecto en el
    registro de eventos y `appsettings.json` lo deja en `Information`. Sin este cambio, cada corrida (arranques,
    400 y 401 provocados a propósito, ingresos) dejaría eventos de prueba mezclados con los del servicio real, en
    el mismo registro en el que se investigan incidentes. No se habilita nada ni se relaja ninguna seguridad: solo
    se escribe menos.
- El proceso de la API recibe un entorno de **lista blanca**: `PATH`, `TEMP`/`TMP`, `HOME`, `USERPROFILE`,
  `SystemRoot`, `DOTNET_ROOT` y similares, más `ASPNETCORE_ENVIRONMENT` y `OPTICA_E2E_DATOS`.
  - No le llegan las variables de configuración de la consola de quien corre la suite: `Kestrel__*`,
    `Logging__*`, `Optica__*`, `Urls`, `*_ENVIRONMENT`.
  - Tampoco le llegan `ProgramData`, `LOCALAPPDATA`, `APPDATA` ni `ALLUSERSPROFILE`. La API no las necesita: las
    rutas van por línea de comandos, y DPAPI (que en Windows cifra las claves de sesión) usa el perfil cargado del
    usuario, no variables de entorno. Está verificado en Windows: claves cifradas con DPAPI y antiforgery
    funcionando sin esas variables.
- **Preparación por archivo.** Cada archivo deja la base de su navegador en un estado conocido en `beforeAll`.
  Para eso usa el comando de operación `--blanquear-contrasena` de la API, que deja la base sin contraseña y sin
  intentos fallidos. Como el comando escribe una base, lleva estas salvaguardas:
  - las rutas van por línea de comandos;
  - antes de blanquear se exige el marcador del harness de este repo en la raíz y la base de esa instancia;
  - después se exige la línea nueva en la auditoría de esa misma instancia.

  Si algo no se cumple, el test se corta con un error explícito. `configuracion.spec.ts`, además, crea su propia contraseña por API. Así cada archivo puede
  correr solo, en cualquier orden, y si uno falla el otro no hereda su estado ni suma ingresos fallidos.
- Se corre con un solo worker, para que los dos archivos nunca usen a la vez la base de un mismo navegador.
  Dentro de cada archivo los tests son un flujo ordenado (`test.describe.serial`): si uno falla, los siguientes del
  mismo archivo no corren. No hay reintentos.
- **Limpieza.** En Linux y macOS, al terminar, el script detiene las instancias y borra la raíz. En Windows,
  Playwright mata el árbol de procesos sin avisar, así que la raíz queda hasta la corrida siguiente. Esa corrida
  borra un directorio viejo solo si se cumplen las tres condiciones siguientes:
  - el nombre es `optica-e2e-` seguido de 6 caracteres alfanuméricos;
  - tiene el marcador del harness de **este** repo;
  - su servidor ya no corre (PID válido y terminado), o el marcador tiene más de un día y su PID no es válido. Un
    servidor que sigue corriendo nunca se borra, por viejo que sea.

  Además tiene que ser un directorio real: un enlace simbólico o una junction con ese nombre no se sigue.

  Solo contiene datos de prueba: una base con una contraseña ficticia y claves de sesión de prueba.

## Requisitos

- Windows 10/11 con **Chrome** y **Edge** instalados (la versión estable vigente o la anterior; ver más abajo).
- **.NET 10 SDK**.
- **Node.js** con npm en el `PATH`, en una versión que cumpla `engines` de `e2e/package.json` y `web/package.json`:
  Node `^22.22.2`, `^24.15.0` o `>=26.0.0`, y npm `>=10.9.8`. Con otra versión, `npm ci` se niega a instalar
  (`engine-strict`).
- Los puertos 5180 y 5181 de `127.0.0.1` libres (5182 si se usa el proyecto auxiliar).
- **Cerrar el servidor de desarrollo de Vite** (`npm run dev` en `web/`) antes de correr la suite. La publicación
  corre `npm ci` en `web/`, que borra y reinstala `web/node_modules`, y eso rompe un Vite abierto.
- No hace falta `npx playwright install`: los proyectos `chrome` y `msedge` usan los navegadores instalados.

## Correr la suite en Windows (PowerShell)

Desde la raíz del repo, en una consola de Windows PowerShell **sin** permisos de administrador. Se usan
`npm.cmd` y `npx.cmd` para que no intervenga la política de ejecución de scripts de PowerShell:

```powershell
Set-Location .\e2e
npm.cmd ci
npm.cmd run test:harness
npx.cmd playwright test
$LASTEXITCODE
```

`npm.cmd run test:harness` prueba el propio harness en unos segundos, sin navegadores ni publicación. Comprueba:
- que solo se aceptan los puertos 5180 a 5182;
- que con un puerto ocupado por otro proceso la suite se niega a correr;
- la verificación de identidad de cada instancia;
- que la limpieza respeta las condiciones, incluido el caso del enlace;
- que el entorno es de lista blanca y las rutas van por línea de comandos;
- las salvaguardas del blanqueo.

`npx.cmd playwright test` debe terminar con `12 passed` y `$LASTEXITCODE` en `0`. Para un solo navegador:
`npx.cmd playwright test --project chrome` (o `--project msedge`). Para un solo archivo:
`npx.cmd playwright test tests/configuracion.spec.ts --project chrome`.

La primera corrida tarda más: `dotnet publish` restaura paquetes y corre `npm ci` en `web/`.

### Informes y trazas: no se comparten

El informe HTML queda en `e2e\playwright-report\` (`npx.cmd playwright show-report`). Cuando algo falla, la
captura y la traza quedan en `e2e\test-results\`. Las trazas guardan todo lo que se tipeó, incluidas las
contraseñas de prueba, además de las cookies y las respuestas de la API. **No se commitean** (están en
`.gitignore`), **no se adjuntan a tickets ni se mandan por correo o chat**. Se borran cuando ya no hacen falta.
Las contraseñas de la suite son ficticias y no pueden reemplazarse por contraseñas reales.

> **Mismo checkout desde WSL y Windows.** La publicación corre `npm ci` en `web/` y `dotnet restore` sobre
> `src/`. Si el repo se usa también desde WSL, después de correr la suite en Windows `web/node_modules` queda con
> los binarios de Windows y `obj/` con rutas de Windows. Conviene correrla desde un clon propio en Windows. Si no,
> al volver a WSL hay que ejecutar `npm ci` en `web/` y `dotnet restore Optica.sln`.

## Versión vigente y versión anterior del navegador

NFR-04 pide la versión estable vigente y la anterior de Chrome y de Edge.

1. **Vigente.** Correr la suite con los navegadores actualizados (`chrome://settings/help` y
   `edge://settings/help` muestran la versión). Anotar las dos versiones junto con el resultado.
2. **Anterior.** Correrla en una PC que todavía tenga la versión anterior, o indicar un ejecutable puntual con
   las variables `OPTICA_E2E_EJECUTABLE_CHROME` y `OPTICA_E2E_EJECUTABLE_EDGE`. Las variables reemplazan al
   navegador instalado solo en esa consola:

   ```powershell
   $env:OPTICA_E2E_EJECUTABLE_CHROME = "C:\navegadores\chrome-anterior\chrome.exe"
   $env:OPTICA_E2E_EJECUTABLE_EDGE   = "C:\navegadores\edge-anterior\msedge.exe"
   npx.cmd playwright test
   Remove-Item Env:\OPTICA_E2E_EJECUTABLE_CHROME, Env:\OPTICA_E2E_EJECUTABLE_EDGE
   ```

   Para Chrome sirve una versión de *Chrome for Testing*. Para Edge, un instalador de la versión anterior del
   canal estable (Microsoft los publica para empresas). Descargarlos solo de los sitios oficiales de Google y
   Microsoft.

## Si falta un navegador

Playwright no busca otro navegador: el proyecto falla con un mensaje explícito, por ejemplo:

```
Error: browserType.launch: Chromium distribution 'msedge' is not found at C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe
Run "npx playwright install msedge"
```

- Instalar el navegador que falta desde su sitio oficial, o con `npx.cmd playwright install msedge` /
  `npx.cmd playwright install chrome` (necesitan permisos de administrador), y volver a correr la suite.
- Mientras tanto se puede correr el otro proyecto (`--project chrome`), pero **AC-18 no está cumplido** hasta
  que la suite pase en los dos.

## Linux / WSL (solo para validar la suite)

En Linux no hay Chrome ni Edge de escritorio. Para comprobar que la suite funciona se puede usar el proyecto
auxiliar `chromium` (puerto 5182). Ese proyecto **no** reemplaza la corrida oficial en Windows y no está activo
por defecto:

```bash
cd e2e
npm ci
npm run test:harness
npx playwright install chromium      # sin --with-deps: no instala nada del sistema
OPTICA_E2E_CHROMIUM=1 npx playwright test --project chromium
```

Si Chromium no arranca porque faltan bibliotecas del sistema (`libnss3`, `libnspr4`, `libasound2`), hay que
instalarlas con el gestor de paquetes de la distribución. Eso queda a criterio de quien administra la máquina.

## Borrar temporales que hayan quedado

Cada corrida borra los temporales de corridas anteriores que cumplen las condiciones de arriba. Para borrarlos a
mano, con la suite detenida:

```powershell
Get-ChildItem $env:TEMP -Directory | Where-Object Name -Match '^optica-e2e-[A-Za-z0-9]{6}$' | Remove-Item -Recurse -Force
```

```bash
find /tmp -maxdepth 1 -type d -regextype posix-extended -regex '.*/optica-e2e-[A-Za-z0-9]{6}' -exec rm -rf {} +
```
