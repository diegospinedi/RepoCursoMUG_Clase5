# Instalación de Óptica Sistema

Procedimiento para instalar el servidor en la PC del local (Windows). El sistema corre como servicio de Windows,
escucha **solo** en `127.0.0.1:5080` y se usa desde un navegador en esa misma PC.

Todos los comandos de este documento son de **Windows PowerShell 5.1** (`powershell.exe`), abierto con
*Ejecutar como administrador*. La continuación de línea es el acento grave (`` ` ``).

## 1. Requisitos

- Windows 10/11 con una cuenta de Administrador.
- Runtime de ASP.NET Core 10 instalado, o una publicación autocontenida.
- Chrome o Edge en su versión estable vigente o la anterior.

## 2. Publicar la aplicación

En la PC de desarrollo, desde la raíz del repo:

```powershell
dotnet publish src/Optica.Api -c Release -o .\publicacion
```

En la PC del local, copiar el contenido de `publicacion` a `C:\Program Files\OpticaSistema\`.

## 3. Directorio de datos

La base (`optica.db`, `optica.db-wal`, `optica.db-shm`), las claves de sesión y la auditoría del blanqueo se
guardan en `%ProgramData%\OpticaSistema\` (valores por defecto de `appsettings.json`):

| Configuración | Ruta por defecto | Contenido |
|---|---|---|
| `Optica:RutaBase` | `%ProgramData%\OpticaSistema` | `optica.db` y sus archivos `-wal` / `-shm` |
| `Optica:DirectorioClaves` | `%ProgramData%\OpticaSistema\claves` | Claves de Data Protection (cifran la cookie de sesión), protegidas con DPAPI |
| `Optica:DirectorioAuditoria` | `%ProgramData%\OpticaSistema\auditoria` | `auditoria-blanqueo.log` |

El servidor **no arranca** si alguna de estas rutas cae dentro de un directorio que contiene `Optica.sln` o
`.git` (el código fuente): así ninguna base, clave o log termina en el repo.

```powershell
New-Item -ItemType Directory -Force -Path "$env:ProgramData\OpticaSistema" | Out-Null
```

## 4. Crear el servicio (sin iniciarlo)

La cuenta virtual `NT SERVICE\OpticaSistema` recién existe cuando se crea el servicio; por eso el servicio se
crea **antes** de asignar permisos y se inicia **después**.

```powershell
sc.exe --% create OpticaSistema binPath= "\"C:\Program Files\OpticaSistema\Optica.Api.exe\"" start= auto obj= "NT SERVICE\OpticaSistema"
sc.exe description OpticaSistema "Óptica Sistema - servidor local"
```

`--%` hace que PowerShell pase la línea tal cual a `sc.exe`; las comillas escapadas dejan la ruta del ejecutable
entre comillas, necesario porque `Program Files` tiene un espacio.

## 5. Permisos del directorio de datos (ACL)

Solo la cuenta del servicio, los Administradores (`S-1-5-32-544`) y SYSTEM (`S-1-5-18`). Se usan SIDs para que
el comando funcione igual en un Windows en español o en inglés.

Este comando es para una **instalación limpia**: `/inheritance:r` quita los permisos heredados y `/grant:r`
reemplaza los de esas tres cuentas, así que el directorio queda solo con estas tres entradas.

```powershell
icacls "$env:ProgramData\OpticaSistema" /inheritance:r `
  /grant:r "NT SERVICE\OpticaSistema:(OI)(CI)M" `
  /grant:r "*S-1-5-32-544:(OI)(CI)F" `
  /grant:r "*S-1-5-18:(OI)(CI)F"
```

Verificar la ACL:

```powershell
icacls "$env:ProgramData\OpticaSistema"
```

La salida tiene que mostrar **exactamente** estas tres entradas (los nombres de los grupos aparecen en el idioma
del sistema) y ninguna más, por ejemplo `Usuarios` o `Usuarios autenticados`:

```
C:\ProgramData\OpticaSistema NT SERVICE\OpticaSistema:(OI)(CI)(M)
                             BUILTIN\Administradores:(OI)(CI)(F)
                             NT AUTHORITY\SYSTEM:(OI)(CI)(F)
```

Con esta ACL, otro usuario de Windows de la PC no puede leer el hash de la contraseña ni modificar la base
(riesgo aceptado AR-02 del threat model), ni ejecutar el blanqueo.

**Reinstalación** (el directorio ya existe y tiene datos): no borrar el directorio ni su contenido, que es la base
del negocio. Detener el servicio (`Stop-Service OpticaSistema`), hacer un respaldo de la base (FEAT-001e), volver
a ejecutar el mismo `icacls` (deja la ACL exactamente en las tres entradas, aunque alguien la haya modificado) y
verificarla. Si el servicio se volvió a crear, el paso 4 tiene que ir antes que este, para que la cuenta
`NT SERVICE\OpticaSistema` exista. Si `icacls` informa archivos que no pudo procesar, repetirlo agregando `/T` para
aplicar la ACL a todos los archivos y subdirectorios existentes.

## 6. Registrar el origen del Visor de eventos

Cuando corre **como servicio de Windows**, el sistema escribe sus eventos (ingresos, fallos, bloqueos, cambios de
contraseña y de configuración) en el registro **Application** con el origen `Optica.Api`. Ese origen solo aplica
al servicio: si se ejecuta `Optica.Api.exe` desde una consola, los mensajes salen en la consola. La cuenta virtual del servicio no tiene permiso para crear
ese origen, así que lo registra un administrador una sola vez:

```powershell
New-EventLog -LogName Application -Source "Optica.Api"
```

Verificar que quedó registrado (tiene que responder `True`):

```powershell
[System.Diagnostics.EventLog]::SourceExists("Optica.Api")
```

## 7. Iniciar el servicio

```powershell
sc.exe start OpticaSistema
(Get-Service OpticaSistema).WaitForStatus('Running', '00:00:30')
```

Si `WaitForStatus` termina sin error, el servicio quedó corriendo. Si a los 30 segundos no llegó a `Running`,
lanza una excepción de tiempo agotado: revisar el Visor de eventos (origen `Optica.Api`).

Las claves de Data Protection quedan cifradas con DPAPI para la cuenta del servicio: si se cambia esa cuenta,
las sesiones abiertas se pierden y hay que volver a ingresar (no se pierde ningún dato).

## 8. Verificar que escucha solo en la PC

```powershell
netstat -ano | Select-String ":5080"
```

Tiene que aparecer únicamente `127.0.0.1:5080`. **No** tiene que aparecer `0.0.0.0:5080`. No hace falta abrir
puertos en el firewall.

**Guarda de loopback.** La ausencia de TLS está aceptada solo porque el tráfico no sale de la PC (AR-01). Si
`Urls` (en `appsettings.json`, en una variable de entorno o en un argumento) se cambia a una dirección que no es
loopback, por ejemplo `http://0.0.0.0:5080`, el servidor se detiene apenas arranca, con código de salida 1, y
registra el error `DireccionFueraDeLoopback`. Lo mismo pasa si el servidor no informa en qué direcciones escucha.
Además, todo pedido que llegue por una interfaz que no sea loopback se rechaza (403) sin procesarse, aun en el
instante entre que el servidor abre el puerto y se detiene.

Cuando corre como servicio, el código de salida 1 no se ve en ninguna consola: se ve en el estado del servicio
(`Get-Service OpticaSistema` muestra `Stopped`; `sc.exe query OpticaSistema` muestra `WIN32_EXIT_CODE`) y en el
Visor de eventos (*Aplicación*, origen `Optica.Api`, evento `DireccionFueraDeLoopback`). Corregir `Urls` a
`http://127.0.0.1:5080` y volver a iniciar el servicio.

## 9. Crear la contraseña al instalar

Mientras no haya contraseña definida, quien abra primero el sistema puede crearla. Por eso, **inmediatamente
después de instalar**:

1. Abrir `http://localhost:5080` en el navegador de la PC.
2. Crear la contraseña de acceso (mínimo 8 caracteres) y comunicarla a las dos operadoras.
3. Ingresar con la contraseña y revisar la pantalla de Configuración (alícuota de IVA, condición fiscal, tope
   de identificación y múltiplo de redondeo).

## 10. Acceso directo

Crear en el escritorio un acceso directo a `http://localhost:5080`. Usar siempre esa dirección: el servidor
rechaza (400) los pedidos que llegan con otro nombre de host.

## 11. Logs y respaldo

- Los eventos del servicio están en *Visor de eventos → Registros de Windows → Aplicación*, origen `Optica.Api`.
  Nunca contienen la contraseña, el hash ni la cookie.
- Respaldo de la base: **no** copiar `optica.db` con una copia de archivo común (la base está en modo WAL y los
  cambios recientes pueden estar en `optica.db-wal`). El procedimiento de respaldo con `VACUUM INTO` se
  documenta en FEAT-001e.

## 12. Olvido de la contraseña

Ver [blanqueo-contrasena.md](blanqueo-contrasena.md).
