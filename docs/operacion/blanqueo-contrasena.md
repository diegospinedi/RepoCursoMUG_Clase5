# Blanqueo de la contraseña

Procedimiento para cuando nadie recuerda la contraseña de acceso (FR-06). Deja el sistema **sin contraseña**:
al abrirlo, se muestra la pantalla para crear una nueva. No se pierde ningún dato de la configuración.

No existe ninguna pantalla ni endpoint web para blanquear la contraseña. Solo se puede hacer desde la PC donde
corre el servidor, con un usuario de Windows que tenga permiso de escritura sobre
`%ProgramData%\OpticaSistema\` (Administradores, según la ACL de la instalación).

Todos los comandos son de **Windows PowerShell 5.1** (`powershell.exe`) abierto con *Ejecutar como
administrador*, igual que en [instalacion.md](instalacion.md).

> **Importante.** Mientras no se cree la contraseña nueva, cualquier usuario o proceso de la PC que abra
> `http://localhost:5080` puede crearla primero y quedarse con el acceso. Hacer el blanqueo solo cuando se pueda
> crear la contraseña nueva **inmediatamente después** (paso 4).

## Pasos

1. En la PC del servidor, abrir Windows PowerShell con *Ejecutar como administrador*.
2. Ir a la carpeta de instalación:

   ```powershell
   Set-Location "C:\Program Files\OpticaSistema"
   ```

3. Ejecutar:

   ```powershell
   .\Optica.Api.exe --blanquear-contrasena
   $LASTEXITCODE
   ```

   No hace falta detener el servicio. Si la base está ocupada en ese momento, el comando espera hasta 5
   segundos. El comando usa el mismo entorno que el servidor (`DOTNET_ENVIRONMENT` o, si no está definido,
   `ASPNETCORE_ENVIRONMENT`), así que trabaja sobre la misma base.

4. **Enseguida**, abrir `http://localhost:5080`: aparece la pantalla de creación de la contraseña. Crear la nueva
   y comunicarla a las operadoras.

## Resultado

| Mensaje en la consola | Código de salida (`$LASTEXITCODE`) | Significado |
|---|---|---|
| `La contraseña se blanqueó. Al abrir el sistema se va a pedir crear una contraseña nueva.` | 0 | Listo. |
| `No había ninguna contraseña definida. ...` | 0 | No había nada que blanquear; el sistema ya pide crearla. |
| `No se pudo blanquear la contraseña. ...` | 1 | No se modificó nada. Ver *Problemas frecuentes*. |
| `La contraseña se blanqueó, pero no quedó registrada en la auditoría.` | 1 | **El blanqueo sí se hizo** (seguir con el paso 4), pero falló la escritura de `auditoria-blanqueo.log`. Revisar los permisos de `%ProgramData%\OpticaSistema\auditoria` y dejar constancia del blanqueo por otro medio. |

Un código 1 no siempre significa que la contraseña sigue igual: leer el mensaje.

Al blanquear, todas las sesiones abiertas se cierran: cualquier navegador que estuviera usando el sistema
vuelve a la pantalla de ingreso.

## Auditoría

Cada ejecución que llega a operar sobre la base, exitosa o no, agrega una línea a
`%ProgramData%\OpticaSistema\auditoria\auditoria-blanqueo.log`:

```
2026-10-16T14:03:21.1234567Z | PC-OPTICA\Administrador | OK - contraseña blanqueada
2026-10-16T14:05:02.7654321Z | PC-OPTICA\Administrador | ERROR - SqliteException: SQLite Error 14: 'unable to open database file'.
```

Formato: `fecha y hora UTC (ISO 8601) | usuario de Windows | resultado`. Si la configuración de rutas es
inválida, el comando termina antes y no escribe auditoría. El archivo no contiene la contraseña ni el hash.
Contiene el nombre del usuario de Windows que ejecutó el comando: tratarlo como dato personal y no compartirlo
fuera del local.

## Qué NO dispara el blanqueo

El blanqueo se activa **solo** con el argumento exacto `--blanquear-contrasena` en la línea de comandos. No se
activa con ninguna clave de `appsettings.json`, ni con variables de entorno, ni con otra forma del argumento
(`--blanquear-contrasena=true`, mayúsculas, etc.). Reiniciar el servicio nunca blanquea la contraseña.

## Problemas frecuentes

| Síntoma | Causa probable | Qué hacer |
|---|---|---|
| `Access to the path ... is denied` o `unable to open database file` | PowerShell no se abrió como administrador | Repetir el paso 1 con *Ejecutar como administrador*. |
| `database is locked` | Otro proceso tiene la base tomada más de 5 segundos | Ejecutar `sc.exe stop OpticaSistema`, repetir el paso 3 y después `sc.exe start OpticaSistema`. |
| `Configuración inválida: Optica:RutaBase ... está dentro del árbol de fuentes` | El `appsettings.json` de la instalación apunta a una carpeta del código fuente | Corregir las rutas en `appsettings.json` (ver [instalacion.md](instalacion.md)). |
