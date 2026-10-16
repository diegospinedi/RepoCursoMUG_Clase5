// Servidor de las pruebas de punta a punta. Lo lanza el `webServer` de playwright.config.ts; no se usa a mano.
//
// 1. Toma la raíz de la corrida de OPTICA_E2E_RAIZ (os.tmpdir()/optica-e2e-XXXXXX, la elige
//    playwright.config.ts), la crea (tiene que NO existir) y deja en ella el marcador del harness.
// 2. Publica la API con el SPA (`dotnet publish`, que corre el target PublicarWeb) en esa raíz.
// 3. Levanta una instancia por proyecto de Playwright, solo en los puertos de E2E (5180-5182), cada una con su
//    propia base, claves y auditoría. Las rutas van por línea de comandos (--Optica:...), que tiene prioridad sobre
//    appsettings*.json; el entorno es de lista blanca con ASPNETCORE_ENVIRONMENT=E2E. Antes de cada arranque
//    comprueba que el puerto esté libre y, después, que quien responde es la instancia propia (arranque-e2e.mjs).
// 4. Recién cuando TODAS las instancias pasaron la verificación escribe en stdout la línea
//    `OPTICA-E2E-LISTO <token>`, que es lo único que Playwright espera (webServer.wait). Playwright no sondea
//    ninguna URL: una instancia ajena en el puerto no puede dar la suite por lista.
// 5. Al recibir SIGTERM/SIGINT (Linux y macOS) detiene las instancias y borra la raíz.
//
// En Windows, Playwright termina el árbol de procesos a la fuerza (taskkill), sin señal: la raíz de esa corrida
// queda y la borra la corrida siguiente (limpiarCorridasAnteriores) o se borra a mano.
//
// Uso: node scripts/servidor-e2e.mjs <nombre>:<puerto> [<nombre>:<puerto> ...]

import { spawn, spawnSync } from 'node:child_process';
import { lstatSync, mkdirSync, readdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import path from 'node:path';
import { esperarArranque, lanzarInstancia, mensajeOcupado, validarPuerto } from './arranque-e2e.mjs';
import {
  ARCHIVO_MARCADOR,
  argumentosDeRutas,
  directorioDatos,
  directorioPublicacion,
  entornoDeLaApi,
  FIRMA_MARCADOR,
  PATRON_DIRECTORIO,
  puertoOcupado,
  raizDeLaCorrida,
  REPO,
  temporalDelSistema,
} from './entorno-e2e.mjs';

const UN_DIA_MS = 24 * 60 * 60 * 1000;
const VARIABLE_TOKEN = 'OPTICA_E2E_TOKEN_LISTO';

const proyectoApi = path.join(REPO, 'src', 'Optica.Api', 'Optica.Api.csproj');

function registrar(mensaje) {
  process.stderr.write(`[servidor-e2e] ${mensaje}\n`);
}

// --- Estado y limpieza -------------------------------------------------------------------------------------

/** Raíz creada por ESTE proceso; solo esa se borra al terminar. */
let raizPropia = null;
const procesos = [];
let terminando = false;

function borrarRaizPropia() {
  if (!raizPropia) {
    return;
  }

  try {
    rmSync(raizPropia, { recursive: true, force: true, maxRetries: 5, retryDelay: 200 });
  } catch (error) {
    registrar(`no se pudo borrar ${raizPropia}: ${error.message}. Se borra en la próxima corrida.`);
  }
}

async function terminar(codigo) {
  if (terminando) {
    return;
  }

  terminando = true;
  // Espera a que las instancias suelten la base antes de borrarla.
  await Promise.all(
    procesos.map(
      (proceso) =>
        new Promise((resolver) => {
          if (proceso.exitCode !== null || proceso.signalCode !== null) {
            resolver();
            return;
          }

          proceso.once('exit', resolver);
          proceso.kill('SIGTERM');
          setTimeout(() => {
            proceso.kill('SIGKILL');
            resolver();
          }, 10_000).unref();
        }),
    ),
  );

  borrarRaizPropia();
  process.exit(codigo);
}

function fallar(mensaje) {
  registrar(`ERROR: ${mensaje}`);
  void terminar(1);
  // Detiene el flujo del arranque mientras terminar() limpia.
  return new Promise(() => {});
}

process.on('SIGTERM', () => void terminar(0));
process.on('SIGINT', () => void terminar(0));

// --- Limpieza de corridas anteriores (S-4) -----------------------------------------------------------------

function procesoVivo(pid) {
  try {
    process.kill(pid, 0);
    return true;
  } catch (error) {
    return error.code === 'EPERM';
  }
}

/**
 * Borra un directorio de una corrida anterior solo si:
 * - es un directorio real (no un enlace simbólico ni una junction) con nombre `optica-e2e-XXXXXX`;
 * - tiene el marcador del harness con la firma y la ruta de ESTE repo;
 * - su servidor terminó (PID entero positivo que no está vivo), o el marcador tiene más de un día Y su PID no es
 *   válido. Un PID válido y vivo nunca se borra, por viejo que sea.
 * Ante cualquier duda, no borra.
 */
function limpiarCorridasAnteriores(temporal, raizActual) {
  for (const entrada of readdirSync(temporal, { withFileTypes: true })) {
    if (!PATRON_DIRECTORIO.test(entrada.name)) {
      continue;
    }

    const directorio = path.join(temporal, entrada.name);
    if (directorio === raizActual) {
      continue;
    }

    try {
      // lstat: un enlace o junction con nombre válido no se sigue (podría apuntar a datos ajenos).
      if (!lstatSync(directorio).isDirectory()) {
        continue;
      }

      const marcador = JSON.parse(readFileSync(path.join(directorio, ARCHIVO_MARCADOR), 'utf8'));
      if (marcador?.firma !== FIRMA_MARCADOR || marcador?.repo !== REPO) {
        continue;
      }

      const pidValido = Number.isSafeInteger(marcador.pid) && marcador.pid > 0;
      const creado = Date.parse(marcador.creado);
      const viejo = Number.isFinite(creado) && Date.now() - creado > UN_DIA_MS;
      const terminado = pidValido && !procesoVivo(marcador.pid);
      if (terminado || (viejo && !pidValido)) {
        rmSync(directorio, { recursive: true, force: true, maxRetries: 3 });
        registrar(`se borró el directorio de una corrida anterior: ${directorio}`);
      }
    } catch {
      // Sin marcador legible: no es del harness (o no se puede asegurar). No se toca.
    }
  }
}

// --- Instancias --------------------------------------------------------------------------------------------

function leerInstancias() {
  const argumentos = process.argv.slice(2);
  if (argumentos.length === 0) {
    throw new Error('falta indicar las instancias (<nombre>:<puerto>).');
  }

  return argumentos.map((argumento) => {
    const [nombre, textoPuerto] = argumento.split(':');
    const puerto = Number(textoPuerto);
    if (!/^[a-z0-9-]+$/.test(nombre ?? '') || !/^\d+$/.test(textoPuerto ?? '')) {
      throw new Error(`instancia inválida: "${argumento}" (formato <nombre>:<puerto>).`);
    }

    validarPuerto(puerto);
    return { nombre, puerto };
  });
}

function leerToken() {
  const token = process.env[VARIABLE_TOKEN];
  if (!/^[0-9a-f]{32}$/.test(token ?? '')) {
    throw new Error(`falta ${VARIABLE_TOKEN} (la define playwright.config.ts).`);
  }

  return token;
}

/** Entorno de `dotnet publish`: el heredado sin configuración de la aplicación ni de npm del que lanzó la suite. */
function entornoDePublicacion() {
  const entorno = {};
  for (const [clave, valor] of Object.entries(process.env)) {
    if (!/^(optica|kestrel|logging|urls|aspnetcore_|dotnet_environment|npm_)/i.test(clave)) {
      entorno[clave] = valor;
    }
  }

  return entorno;
}

// --- Arranque ----------------------------------------------------------------------------------------------

let lista;
let raiz;
let token;
try {
  lista = leerInstancias();
  raiz = raizDeLaCorrida();
  token = leerToken();
} catch (error) {
  await fallar(error.message);
}

limpiarCorridasAnteriores(temporalDelSistema(), raiz);

// Primer control de puertos, antes de publicar (rápido y sin crear nada).
for (const instancia of lista) {
  if (await puertoOcupado(instancia.puerto)) {
    await fallar(mensajeOcupado(instancia.puerto));
  }
}

try {
  mkdirSync(raiz); // Sin recursive: falla si ya existe (nunca se reutiliza una raíz).
} catch (error) {
  await fallar(`no se pudo crear la raíz de la corrida ${raiz}: ${error.message}`);
}

raizPropia = raiz;
writeFileSync(
  path.join(raiz, ARCHIVO_MARCADOR),
  JSON.stringify({ firma: FIRMA_MARCADOR, repo: REPO, pid: process.pid, creado: new Date().toISOString() }),
);
registrar(`directorio de la corrida: ${raiz}`);

const publicacion = directorioPublicacion(raiz);
registrar('publicando la API y el SPA (dotnet publish)...');
const publicado = spawnSync(
  'dotnet',
  ['publish', proyectoApi, '--configuration', 'Release', '--output', publicacion, '--nologo'],
  { cwd: REPO, env: entornoDePublicacion(), stdio: ['ignore', 'inherit', 'inherit'], shell: false },
);
if (publicado.status !== 0) {
  await fallar(`dotnet publish terminó con código ${publicado.status ?? publicado.error?.message}.`);
}

for (const instancia of lista) {
  const datos = directorioDatos(raiz, instancia.nombre);
  try {
    // Segundo control, justo antes de cada arranque: el puerto pudo ocuparse durante la publicación.
    const proceso = await lanzarInstancia({
      puerto: instancia.puerto,
      puertoOcupado,
      lanzar: () =>
        spawn(
          'dotnet',
          [
            path.join(publicacion, 'Optica.Api.dll'),
            '--urls',
            `http://127.0.0.1:${instancia.puerto}`,
            ...argumentosDeRutas(datos, raiz),
          ],
          { cwd: publicacion, env: entornoDeLaApi(datos, raiz), stdio: ['ignore', 'pipe', 'inherit'], shell: false },
        ),
    });
    procesos.push(proceso);
    proceso.once('exit', (codigo, senal) => {
      if (!terminando) {
        registrar(`la instancia ${instancia.nombre} terminó inesperadamente (código ${codigo}, señal ${senal}).`);
        void terminar(1);
      }
    });

    await esperarArranque({
      nombre: instancia.nombre,
      puerto: instancia.puerto,
      proceso,
      datos,
      // Las líneas de la instancia llevan su prefijo: nunca pueden coincidir con la señal de "listo".
      alLeer: (linea) => process.stdout.write(`[${instancia.nombre}] ${linea}\n`),
    });
  } catch (error) {
    await fallar(error.message);
  }

  registrar(`instancia ${instancia.nombre} verificada en http://127.0.0.1:${instancia.puerto} (datos en ${datos})`);
}

// Señal de "listo" para Playwright (webServer.wait.stdout): solo después de verificar todas las instancias.
process.stdout.write(`OPTICA-E2E-LISTO ${token}\n`);
