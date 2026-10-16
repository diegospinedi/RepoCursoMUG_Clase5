// Piezas comunes del harness de punta a punta: las usan playwright.config.ts, scripts/servidor-e2e.mjs y la
// preparación de los tests (blanqueo). No tienen dependencias fuera de Node.

import { spawnSync } from 'node:child_process';
import { randomInt } from 'node:crypto';
import { existsSync, readFileSync, realpathSync } from 'node:fs';
import { connect } from 'node:net';
import { tmpdir } from 'node:os';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

/** Raíz del repo (e2e/scripts/../..). */
export const REPO = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..', '..');

/** Nombre del directorio de una corrida: `optica-e2e-` y 6 caracteres alfanuméricos. */
export const PATRON_DIRECTORIO = /^optica-e2e-[A-Za-z0-9]{6}$/;

/** Marcador que el harness escribe en el directorio de cada corrida. Sin él, el directorio no es del harness. */
export const ARCHIVO_MARCADOR = 'harness-e2e.json';
export const FIRMA_MARCADOR = 'optica-e2e-harness';

/** Variable con la raíz de la corrida. La define playwright.config.ts y la heredan el servidor y los workers. */
export const VARIABLE_RAIZ = 'OPTICA_E2E_RAIZ';

const ALFANUMERICOS = 'ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789';

/**
 * Directorio temporal del sistema, con la ruta real: sin enlaces y, en Windows, sin nombres cortos 8.3
 * (`realpathSync.native` usa la API del sistema operativo, que los expande).
 */
export function temporalDelSistema() {
  return realpathSync.native(tmpdir());
}

/** `hijo` está estrictamente dentro de `padre` (también entre unidades distintas de Windows). */
export function estaDentro(padre, hijo) {
  const relativa = path.relative(padre, hijo);
  return relativa !== '' && !relativa.startsWith('..') && !path.isAbsolute(relativa);
}

/** Ruta (todavía no creada) para el directorio de una corrida nueva. */
export function nuevaRaiz() {
  let sufijo = '';
  for (let i = 0; i < 6; i++) {
    sufijo += ALFANUMERICOS[randomInt(ALFANUMERICOS.length)];
  }

  return path.join(temporalDelSistema(), `optica-e2e-${sufijo}`);
}

/**
 * Valida la raíz de la corrida: nombre `optica-e2e-XXXXXX`, directamente dentro del temporal del sistema y fuera
 * del repo. Devuelve la ruta o lanza un error.
 */
export function validarRaiz(raiz) {
  if (typeof raiz !== 'string' || raiz.trim() === '' || !path.isAbsolute(raiz)) {
    throw new Error(`${VARIABLE_RAIZ} tiene que ser una ruta absoluta (valor: "${raiz ?? ''}").`);
  }

  const temporal = temporalDelSistema();
  if (path.dirname(raiz) !== temporal || !PATRON_DIRECTORIO.test(path.basename(raiz))) {
    throw new Error(`${VARIABLE_RAIZ} tiene que ser ${temporal}${path.sep}optica-e2e-XXXXXX (valor: "${raiz}").`);
  }

  if (estaDentro(REPO, raiz)) {
    throw new Error(`${VARIABLE_RAIZ} (${raiz}) está dentro del repo.`);
  }

  return raiz;
}

/** Raíz de la corrida en curso, tomada de la variable de entorno. */
export function raizDeLaCorrida() {
  return validarRaiz(process.env[VARIABLE_RAIZ]);
}

export function directorioPublicacion(raiz) {
  return path.join(raiz, 'publicacion');
}

/** Directorio de datos (base, claves y auditoría) de una instancia: uno por proyecto de Playwright. */
export function directorioDatos(raiz, instancia) {
  if (!/^[a-z0-9-]+$/.test(instancia)) {
    throw new Error(`nombre de instancia inválido: "${instancia}".`);
  }

  return path.join(raiz, instancia);
}

/**
 * S-8: el directorio de datos de una instancia es un hijo directo de la raíz de ESTA corrida (que a su vez está en
 * el temporal del sistema y fuera del repo). Nunca %ProgramData%, el repo ni otra corrida.
 */
export function validarDatos(datos, raiz) {
  validarRaiz(raiz);
  if (typeof datos !== 'string' || datos.trim() === '' || !path.isAbsolute(datos)) {
    throw new Error(`OPTICA_E2E_DATOS tiene que ser una ruta absoluta (valor: "${datos ?? ''}").`);
  }

  if (path.dirname(datos) !== raiz || !/^[a-z0-9-]+$/.test(path.basename(datos))) {
    throw new Error(`OPTICA_E2E_DATOS (${datos}) tiene que estar dentro de la raíz de esta corrida (${raiz}).`);
  }

  return datos;
}

/**
 * Rutas de la API para una instancia, como argumentos de línea de comandos. La línea de comandos tiene prioridad
 * sobre appsettings*.json y las variables de entorno: aunque faltara appsettings.E2E.json o la variable, la API
 * usa estas rutas y nunca las de appsettings.json (%ProgramData%).
 */
export function argumentosDeRutas(datos, raiz) {
  validarDatos(datos, raiz);
  return [
    `--Optica:RutaBase=${path.join(datos, 'base')}`,
    `--Optica:DirectorioClaves=${path.join(datos, 'claves')}`,
    `--Optica:DirectorioAuditoria=${path.join(datos, 'auditoria')}`,
  ];
}

// Variables que se heredan al proceso de la API (lista blanca, comparación sin distinguir mayúsculas, como en
// Windows). Todo lo demás se descarta: así no se cuelan configuraciones como Kestrel__*, Logging__*, Optica__*,
// Urls o *_ENVIRONMENT del entorno de quien corre la suite.
const VARIABLES_HEREDADAS = new Set(
  [
    // Comunes
    'PATH',
    'TEMP',
    'TMP',
    'TMPDIR',
    'HOME',
    'USER',
    'LOGNAME',
    'LANG',
    'LC_ALL',
    'TZ',
    // Windows
    'PATHEXT',
    'SystemRoot',
    'SystemDrive',
    'windir',
    'ComSpec',
    'USERPROFILE',
    'USERNAME',
    'USERDOMAIN',
    'HOMEDRIVE',
    'HOMEPATH',
    // Sin LOCALAPPDATA, APPDATA, ProgramData ni ALLUSERSPROFILE: la API no las necesita (las rutas van por línea
    // de comandos y DPAPI usa el perfil cargado del usuario, no variables de entorno; verificado en Windows).
    'ProgramFiles',
    'ProgramFiles(x86)',
    'ProgramW6432',
    'CommonProgramFiles',
    'CommonProgramFiles(x86)',
    'CommonProgramW6432',
    'PROCESSOR_ARCHITECTURE',
    'NUMBER_OF_PROCESSORS',
    'OS',
    // .NET (ubicación del runtime y de la caché del CLI; ninguna es configuración de la aplicación)
    'DOTNET_ROOT',
    'DOTNET_ROOT(x86)',
    'DOTNET_ROOT_X64',
    'DOTNET_CLI_HOME',
    'DOTNET_CLI_TELEMETRY_OPTOUT',
    'DOTNET_NOLOGO',
  ].map((nombre) => nombre.toLowerCase()),
);

/** Entorno del proceso de la API (instancia o blanqueo): lista blanca + las variables propias del harness. */
export function entornoDeLaApi(datos, raiz) {
  validarDatos(datos, raiz);
  const entorno = {};
  for (const [clave, valor] of Object.entries(process.env)) {
    if (valor !== undefined && VARIABLES_HEREDADAS.has(clave.toLowerCase())) {
      entorno[clave] = valor;
    }
  }

  entorno.ASPNETCORE_ENVIRONMENT = 'E2E';
  entorno.OPTICA_E2E_DATOS = datos;
  return entorno;
}

/** Hay algo aceptando conexiones TCP en 127.0.0.1:puerto (cualquier proceso, responda HTTP o no). */
export function puertoOcupado(puerto) {
  return new Promise((resolver) => {
    const socket = connect({ host: '127.0.0.1', port: puerto });
    const terminar = (ocupado) => {
      socket.destroy();
      resolver(ocupado);
    };
    socket.setTimeout(1000, () => terminar(true));
    socket.once('connect', () => terminar(true));
    socket.once('error', (error) => terminar(error.code !== 'ECONNREFUSED'));
  });
}

/** El directorio tiene el marcador del harness con la firma y la ruta de ESTE repo. */
export function tieneMarcadorPropio(directorio) {
  try {
    const marcador = JSON.parse(readFileSync(path.join(directorio, ARCHIVO_MARCADOR), 'utf8'));
    return marcador?.firma === FIRMA_MARCADOR && marcador?.repo === REPO;
  } catch {
    return false;
  }
}

function lineasDe(archivo) {
  return existsSync(archivo) ? readFileSync(archivo, 'utf8').split(/\r?\n/).filter((linea) => linea !== '') : [];
}

/**
 * Blanquea la contraseña de la base de una instancia con el comando de operación de la API
 * (`--blanquear-contrasena`, FR-06): la base queda sin contraseña y sin intentos fallidos. No abre ningún puerto.
 *
 * Salvaguardas (datos reales): las rutas van por línea de comandos; antes se exige el marcador del harness en la
 * raíz y la base de la instancia; después, la línea nueva en la auditoría de ESA instancia. Si algo no se cumple,
 * lanza un error y el test no sigue.
 */
export function blanquearContrasena(instancia) {
  const raiz = raizDeLaCorrida();
  if (!tieneMarcadorPropio(raiz)) {
    throw new Error(`La raíz ${raiz} no tiene el marcador del harness de este repo: no se blanquea nada.`);
  }

  const datos = directorioDatos(raiz, instancia);
  const base = path.join(datos, 'base', 'optica.db');
  if (!existsSync(base)) {
    throw new Error(`No existe la base de la instancia ${instancia} (${base}): no se blanquea nada.`);
  }

  const auditoria = path.join(datos, 'auditoria', 'auditoria-blanqueo.log');
  const lineasAntes = lineasDe(auditoria).length;

  const publicacion = directorioPublicacion(raiz);
  const resultado = spawnSync(
    'dotnet',
    [path.join(publicacion, 'Optica.Api.dll'), '--blanquear-contrasena', ...argumentosDeRutas(datos, raiz)],
    {
      cwd: publicacion,
      env: entornoDeLaApi(datos, raiz),
      encoding: 'utf8',
      shell: false,
      timeout: 60_000,
    },
  );
  if (resultado.status !== 0) {
    throw new Error(
      `El blanqueo de la instancia ${instancia} terminó con código ${resultado.status ?? resultado.error?.message}: ` +
        `${resultado.stderr ?? ''}`,
    );
  }

  const nuevas = lineasDe(auditoria).slice(lineasAntes);
  if (nuevas.length !== 1 || !/ \| OK - /.test(nuevas[0])) {
    throw new Error(
      `El blanqueo de la instancia ${instancia} no dejó su línea en ${auditoria}: pudo haber usado otra base. ` +
        'Se corta la corrida.',
    );
  }
}
