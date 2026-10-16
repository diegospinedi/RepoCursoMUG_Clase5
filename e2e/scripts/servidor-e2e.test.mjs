// Pruebas del harness de punta a punta (no de la aplicación): `npm run test:harness` en e2e/.
// No publican ni levantan la API: cada caso termina antes de `dotnet publish` o usa dobles. Nunca usan el 5080.

import assert from 'node:assert/strict';
import { spawn, spawnSync } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, rmSync, symlinkSync, writeFileSync } from 'node:fs';
import { createServer } from 'node:http';
import path from 'node:path';
import { PassThrough } from 'node:stream';
import { after, before, beforeEach, describe, test } from 'node:test';
import { fileURLToPath } from 'node:url';
import { esperarArranque, lanzarInstancia, patronEscucha, PUERTOS_E2E, validarPuerto } from './arranque-e2e.mjs';
import {
  ARCHIVO_MARCADOR,
  argumentosDeRutas,
  blanquearContrasena,
  entornoDeLaApi,
  FIRMA_MARCADOR,
  nuevaRaiz,
  puertoOcupado,
  REPO,
  temporalDelSistema,
  validarDatos,
  validarRaiz,
} from './entorno-e2e.mjs';

const E2E = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '..');
const SERVIDOR = path.join(E2E, 'scripts', 'servidor-e2e.mjs');
const CLI_PLAYWRIGHT = path.join(E2E, 'node_modules', '@playwright', 'test', 'cli.js');
const BASE_VACIA = { contrasenaDefinida: false, bloqueadoHasta: null, sesionActiva: false };

/** Servidor ajeno que ocupa un puerto y responde 200 con `cuerpo` a todo, como lo haría otra instancia. */
async function servidorAjeno(puerto, cuerpo = { ...BASE_VACIA, contrasenaDefinida: true }) {
  assert.equal(await puertoOcupado(puerto), false, `el puerto ${puerto} tiene que estar libre para esta prueba`);
  const servidor = createServer((_pedido, respuesta) => {
    respuesta.writeHead(200, { 'Content-Type': 'application/json' });
    respuesta.end(JSON.stringify(cuerpo));
  });
  await new Promise((resolver, rechazar) => {
    servidor.once('error', rechazar);
    servidor.listen(puerto, '127.0.0.1', resolver);
  });
  return servidor;
}

function sinVariable(entorno, nombre) {
  const copia = { ...entorno };
  delete copia[nombre];
  return copia;
}

function entornoDelScript(raiz) {
  return { ...process.env, OPTICA_E2E_RAIZ: raiz, OPTICA_E2E_TOKEN_LISTO: randomBytes(16).toString('hex') };
}

function correrServidor(argumentos, raiz) {
  return spawnSync(process.execPath, [SERVIDOR, ...argumentos], {
    cwd: E2E,
    env: entornoDelScript(raiz),
    encoding: 'utf8',
    timeout: 60_000,
  });
}

function correrAsincrono(comando, argumentos, entorno) {
  return new Promise((resolver, rechazar) => {
    const hijo = spawn(comando, argumentos, { cwd: E2E, env: entorno });
    let stdout = '';
    let stderr = '';
    hijo.stdout.on('data', (parte) => (stdout += parte));
    hijo.stderr.on('data', (parte) => (stderr += parte));
    const limite = setTimeout(() => hijo.kill('SIGKILL'), 120_000);
    hijo.once('error', rechazar);
    hijo.once('close', (status) => {
      clearTimeout(limite);
      resolver({ status, stdout, stderr });
    });
  });
}

// --- Puertos (B-1, W-4) ------------------------------------------------------------------------------------

describe('puertos de E2E', () => {
  test('solo se aceptan 5180, 5181 y 5182', () => {
    assert.deepEqual([...PUERTOS_E2E].sort(), [5180, 5181, 5182]);
    for (const puerto of [5180, 5181, 5182]) {
      assert.equal(validarPuerto(puerto), puerto);
    }

    for (const puerto of [5080, 5081, 5183, 8080, 0]) {
      assert.throws(() => validarPuerto(puerto), /no es de E2E/, `el ${puerto} se rechaza`);
    }
  });

  for (const argumento of ['chrome:5080', 'chrome:5183', 'chrome:8080']) {
    test(`el script rechaza ${argumento} sin crear nada`, () => {
      const raiz = nuevaRaiz();
      const resultado = correrServidor([argumento], raiz);

      assert.equal(resultado.status, 1);
      assert.match(resultado.stderr, /no es de E2E/);
      assert.equal(existsSync(raiz), false);
    });
  }
});

describe('puerto ocupado: la suite se niega a correr', () => {
  let ajeno;
  before(async () => {
    ajeno = await servidorAjeno(5181);
  });
  after(() => ajeno?.close());

  test('el script no publica ni arranca nada si un puerto de E2E ya está en uso', () => {
    const raiz = nuevaRaiz();
    const resultado = correrServidor(['chrome:5180', 'msedge:5181'], raiz);

    assert.equal(resultado.status, 1);
    assert.match(resultado.stderr, /el puerto 5181 de 127\.0\.0\.1 ya está en uso por otro proceso/);
    assert.doesNotMatch(resultado.stderr, /publicando/);
    assert.doesNotMatch(resultado.stdout, /OPTICA-E2E-LISTO/);
    assert.equal(existsSync(raiz), false, 'no se crea la raíz de la corrida');
  });
});

describe('Playwright con el puerto del primer proyecto ocupado', () => {
  let ajeno;
  before(async () => {
    ajeno = await servidorAjeno(5180, BASE_VACIA);
  });
  after(() => ajeno?.close());

  test('npx playwright test se niega a correr aunque el ajeno parezca una base vacía', async () => {
    // Asíncrono: el servidor ajeno vive en este mismo proceso y tiene que poder responder mientras tanto.
    const resultado = await correrAsincrono(
      process.execPath,
      [CLI_PLAYWRIGHT, 'test', '--project', 'chrome', '--reporter=line'],
      sinVariable(sinVariable(process.env, 'OPTICA_E2E_RAIZ'), 'OPTICA_E2E_TOKEN_LISTO'),
    );
    const salida = `${resultado.stdout}${resultado.stderr}`;

    assert.notEqual(resultado.status, 0);
    assert.match(salida, /el puerto 5180 de 127\.0\.0\.1 ya está en uso por otro proceso/);
    assert.doesNotMatch(salida, /publicando|passed/);
  });
});

test('lanzarInstancia: con el puerto ocupado justo antes del arranque no lanza nada', async () => {
  let lanzado = false;
  await assert.rejects(
    lanzarInstancia({ puerto: 5182, puertoOcupado: async () => true, lanzar: () => (lanzado = true) }),
    /el puerto 5182 de 127\.0\.0\.1 ya está en uso/,
  );
  assert.equal(lanzado, false);

  const resultado = await lanzarInstancia({ puerto: 5182, puertoOcupado: async () => false, lanzar: () => 'lanzado' });
  assert.equal(resultado, 'lanzado');
  await assert.rejects(
    lanzarInstancia({ puerto: 5080, puertoOcupado: async () => false, lanzar: () => 'no' }),
    /no es de E2E/,
  );
});

// --- Verificación de identidad (W-2) -----------------------------------------------------------------------

describe('esperarArranque: solo acepta a la instancia propia', () => {
  const datos = path.join(temporalDelSistema(), 'no-se-usa', 'chromium');
  const LINEA_ESCUCHA = '      Now listening on: http://127.0.0.1:5182';
  let ajeno;
  let proceso;

  beforeEach(() => {
    proceso = { stdout: new PassThrough(), exitCode: null, signalCode: null };
  });
  after(() => ajeno?.close());

  async function conAjeno(cuerpo) {
    await new Promise((resolver) => (ajeno ? ajeno.close(resolver) : resolver()));
    ajeno = await servidorAjeno(5182, cuerpo);
  }

  function esperarCon(opciones = {}) {
    return esperarArranque({
      nombre: 'chromium',
      puerto: 5182,
      proceso,
      datos,
      existe: () => true,
      plazoMs: 1500,
      estabilidadMs: 200,
      intervaloMs: 50,
      ...opciones,
    });
  }

  test('la línea de escucha se compara exacta', () => {
    const patron = patronEscucha(5182);
    assert.equal(patron.test(LINEA_ESCUCHA), true);
    assert.equal(patron.test('Now listening on: http://127.0.0.1:5182'), true);
    assert.equal(patron.test('      Now listening on: http://127.0.0.1:51820'), false);
    assert.equal(patron.test('[ajeno] Now listening on: http://127.0.0.1:5182'), false);
    assert.equal(patron.test('      Now listening on: http://0.0.0.0:5182'), false);
    assert.equal(patron.test('      Now listening on: http://127.0.0.1:5182/x'), false);
  });

  test('control: con escucha propia, base vacía, base creada y proceso vivo, se acepta', async () => {
    await conAjeno(BASE_VACIA);
    proceso.stdout.write(`${LINEA_ESCUCHA}\n`);
    await esperarCon();
  });

  test('sin "Now listening" del proceso propio, un 200 ajeno no se acepta', async () => {
    await conAjeno(BASE_VACIA);
    proceso.stdout.write('info: arrancando...\n');
    await assert.rejects(esperarCon(), /no respondió como propia/);
  });

  test('una línea parecida pero no exacta tampoco cuenta', async () => {
    await conAjeno(BASE_VACIA);
    proceso.stdout.write('[otro] Now listening on: http://127.0.0.1:5182\n');
    proceso.stdout.write('      Now listening on: http://127.0.0.1:51820\n');
    await assert.rejects(esperarCon(), /no respondió como propia/);
  });

  test('si quien responde tiene contraseña definida, se rechaza', async () => {
    await conAjeno({ ...BASE_VACIA, contrasenaDefinida: true });
    proceso.stdout.write(`${LINEA_ESCUCHA}\n`);
    await assert.rejects(esperarCon(), /responde un servidor con datos/);
  });

  test('si quien responde tiene sesión activa, se rechaza', async () => {
    await conAjeno({ ...BASE_VACIA, sesionActiva: true });
    proceso.stdout.write(`${LINEA_ESCUCHA}\n`);
    await assert.rejects(esperarCon(), /responde un servidor con datos/);
  });

  test('si la base propia no existe, se rechaza', async () => {
    await conAjeno(BASE_VACIA);
    proceso.stdout.write(`${LINEA_ESCUCHA}\n`);
    let consultada = '';
    await assert.rejects(
      esperarCon({ existe: (ruta) => ((consultada = ruta), false) }),
      /no creó su base/,
    );
    assert.equal(consultada, path.join(datos, 'base', 'optica.db'));
  });

  test('si el proceso cae durante la ventana de estabilidad, se rechaza', async () => {
    await conAjeno(BASE_VACIA);
    proceso.stdout.write(`${LINEA_ESCUCHA}\n`);
    setTimeout(() => (proceso.exitCode = 134), 150);
    await assert.rejects(esperarCon({ estabilidadMs: 400 }), /no quedó corriendo/);
  });

  test('si el proceso termina antes de responder, se rechaza', async () => {
    proceso.exitCode = 1;
    await assert.rejects(esperarCon(), /terminó al arrancar/);
  });
});

// --- Limpieza de corridas anteriores (S-4, W-5) ------------------------------------------------------------

describe('limpieza de corridas anteriores', () => {
  const temporal = temporalDelSistema();
  const pidMuerto = spawnSync(process.execPath, ['-e', '']).pid;
  const hace2Dias = new Date(Date.now() - 2 * 24 * 60 * 60 * 1000).toISOString();
  const ahora = new Date().toISOString();
  const casos = [];
  const extras = [];

  const sufijo = () => path.basename(nuevaRaiz()).slice(-6);
  const propio = (pid, creado) => ({ firma: FIRMA_MARCADOR, repo: REPO, pid, creado });

  function caso(descripcion, nombre, marcador, seBorra) {
    const directorio = path.join(temporal, nombre);
    casos.push({ descripcion, directorio, seBorra });
    mkdirSync(directorio);
    if (marcador !== null) {
      writeFileSync(path.join(directorio, ARCHIVO_MARCADOR), JSON.stringify(marcador));
    }
  }

  let ajeno;
  before(async () => {
    caso('marcador propio y PID terminado', `optica-e2e-${sufijo()}`, propio(pidMuerto, ahora), true);
    caso('marcador propio, PID inválido y más de un día', `optica-e2e-${sufijo()}`, propio('abc', hace2Dias), true);
    caso('sin marcador', `optica-e2e-${sufijo()}`, null, false);
    caso('marcador propio y PID inválido, reciente', `optica-e2e-${sufijo()}`, propio(-1, ahora), false);
    caso('marcador propio y PID vivo, reciente', `optica-e2e-${sufijo()}`, propio(process.pid, ahora), false);
    caso('marcador propio y PID vivo, de más de un día', `optica-e2e-${sufijo()}`, propio(process.pid, hace2Dias), false);
    caso('marcador de otro repo', `optica-e2e-${sufijo()}`, { ...propio(pidMuerto, ahora), repo: '/otro/repo' }, false);
    caso('marcador sin firma', `optica-e2e-${sufijo()}`, { repo: REPO, pid: pidMuerto, creado: ahora }, false);
    caso('nombre fuera del patrón', `optica-e2e-${sufijo()}x`, propio(pidMuerto, ahora), false);

    // Enlace con nombre válido que apunta a un directorio ajeno con un marcador "borrable": no se sigue.
    const destino = path.join(temporal, `ajeno-destino-${sufijo()}`);
    mkdirSync(destino);
    writeFileSync(path.join(destino, ARCHIVO_MARCADOR), JSON.stringify(propio(pidMuerto, ahora)));
    writeFileSync(path.join(destino, 'dato-ajeno.txt'), 'no se borra');
    const enlace = path.join(temporal, `optica-e2e-${sufijo()}`);
    symlinkSync(destino, enlace, process.platform === 'win32' ? 'junction' : 'dir');
    extras.push(destino, enlace);
    casos.push({ descripcion: 'enlace: el enlace se conserva', directorio: enlace, seBorra: false });
    casos.push({ descripcion: 'enlace: el destino ajeno se conserva', directorio: destino, seBorra: false });
    casos.push({
      descripcion: 'enlace: el contenido ajeno se conserva',
      directorio: path.join(destino, 'dato-ajeno.txt'),
      seBorra: false,
    });

    // Puerto ocupado: el script limpia, detecta el puerto y termina sin publicar.
    ajeno = await servidorAjeno(5181);
    correrServidor(['msedge:5181'], nuevaRaiz());
  });
  after(() => {
    ajeno?.close();
    for (const ruta of [...extras.reverse(), ...casos.map(({ directorio }) => directorio)]) {
      rmSync(ruta, { recursive: true, force: true });
    }
  });

  test('borra solo los directorios reales del harness de este repo con el servidor terminado (o PID inválido y viejos)', () => {
    for (const { descripcion, directorio, seBorra } of casos) {
      assert.equal(existsSync(directorio), !seBorra, `${descripcion}: ${seBorra ? 'se borra' : 'se conserva'}`);
    }
  });
});

// --- Entorno y rutas de la API (S-5, S-8, W-1) -------------------------------------------------------------

describe('entorno y rutas de la API', () => {
  test('OPTICA_E2E_DATOS tiene que ser un hijo directo de la raíz de esta corrida', () => {
    const raiz = nuevaRaiz();
    const otra = nuevaRaiz();
    assert.doesNotThrow(() => validarDatos(path.join(raiz, 'chrome'), raiz));
    assert.throws(() => validarDatos('', raiz), /ruta absoluta/);
    assert.throws(() => validarDatos('datos/relativos', raiz), /ruta absoluta/);
    assert.throws(() => validarDatos(path.join(otra, 'chrome'), raiz), /raíz de esta corrida/);
    assert.throws(() => validarDatos(path.join(raiz, 'chrome', 'mas'), raiz), /raíz de esta corrida/);
    assert.throws(() => validarDatos(temporalDelSistema(), raiz), /raíz de esta corrida/);
    assert.throws(() => validarDatos(path.join(REPO, 'datos'), raiz), /raíz de esta corrida/);
    assert.throws(() => validarDatos(path.resolve('/', 'ProgramData', 'OpticaSistema'), raiz), /raíz de esta corrida/);
    assert.throws(() => validarDatos(path.join(raiz, 'chrome'), path.join(REPO, 'optica-e2e-abc123')), /XXXXXX/);
  });

  test('la raíz de la corrida tiene el nombre del harness y está directamente en el temporal', () => {
    assert.throws(() => validarRaiz(undefined), /ruta absoluta/);
    assert.throws(() => validarRaiz(path.join(temporalDelSistema(), 'otro-nombre')), /optica-e2e-XXXXXX/);
    assert.throws(() => validarRaiz(path.join(REPO, 'optica-e2e-abc123')), /optica-e2e-XXXXXX/);
    assert.doesNotThrow(() => validarRaiz(nuevaRaiz()));
  });

  test('las rutas van por línea de comandos y apuntan dentro del directorio de datos', () => {
    const raiz = nuevaRaiz();
    const datos = path.join(raiz, 'chrome');
    assert.deepEqual(argumentosDeRutas(datos, raiz), [
      `--Optica:RutaBase=${path.join(datos, 'base')}`,
      `--Optica:DirectorioClaves=${path.join(datos, 'claves')}`,
      `--Optica:DirectorioAuditoria=${path.join(datos, 'auditoria')}`,
    ]);
    assert.throws(() => argumentosDeRutas(path.join(nuevaRaiz(), 'chrome'), raiz), /raíz de esta corrida/);
  });

  test('solo hereda la lista blanca: nada de Kestrel__*, Logging__*, Optica__*, Urls, *_ENVIRONMENT ni carpetas de datos', () => {
    const anteriores = { ...process.env };
    Object.assign(process.env, {
      Kestrel__Endpoints__Http__Url: 'http://0.0.0.0:80',
      Logging__LogLevel__Default: 'Trace',
      Optica__RutaBase: 'C:\\ProgramData\\OpticaSistema',
      Urls: 'http://0.0.0.0:5080',
      ASPNETCORE_URLS: 'http://0.0.0.0:5080',
      DOTNET_ENVIRONMENT: 'Production',
      ConnectionStrings__Base: 'Data Source=real.db',
      ProgramData: 'C:\\ProgramData',
      APPDATA: 'C:\\Users\\x\\AppData\\Roaming',
      LOCALAPPDATA: 'C:\\Users\\x\\AppData\\Local',
      ALLUSERSPROFILE: 'C:\\ProgramData',
    });
    try {
      const raiz = nuevaRaiz();
      const datos = path.join(raiz, 'chrome');
      const entorno = entornoDeLaApi(datos, raiz);
      const claves = Object.keys(entorno).map((clave) => clave.toLowerCase());

      for (const prohibida of [
        'kestrel__endpoints__http__url',
        'logging__loglevel__default',
        'optica__rutabase',
        'urls',
        'aspnetcore_urls',
        'dotnet_environment',
        'connectionstrings__base',
        'programdata',
        'appdata',
        'localappdata',
        'allusersprofile',
      ]) {
        assert.equal(claves.includes(prohibida), false, `${prohibida} no se hereda`);
      }

      assert.equal(entorno.ASPNETCORE_ENVIRONMENT, 'E2E');
      assert.equal(entorno.OPTICA_E2E_DATOS, datos);
      assert.ok(claves.includes('path'), 'PATH se hereda');
    } finally {
      for (const clave of Object.keys(process.env)) {
        if (!(clave in anteriores)) {
          delete process.env[clave];
        }
      }
      Object.assign(process.env, anteriores);
    }
  });

  describe('blanqueo: salvaguardas previas', () => {
    let raiz;
    let anterior;
    before(() => {
      anterior = process.env.OPTICA_E2E_RAIZ;
      raiz = nuevaRaiz();
      process.env.OPTICA_E2E_RAIZ = raiz;
      mkdirSync(path.join(raiz, 'chrome', 'base'), { recursive: true });
    });
    after(() => {
      rmSync(raiz, { recursive: true, force: true });
      if (anterior === undefined) {
        delete process.env.OPTICA_E2E_RAIZ;
      } else {
        process.env.OPTICA_E2E_RAIZ = anterior;
      }
    });

    test('sin el marcador del harness en la raíz, no blanquea', () => {
      assert.throws(() => blanquearContrasena('chrome'), /no tiene el marcador del harness/);
    });

    test('con un marcador de otro repo, no blanquea', () => {
      writeFileSync(
        path.join(raiz, ARCHIVO_MARCADOR),
        JSON.stringify({ firma: FIRMA_MARCADOR, repo: '/otro/repo', pid: process.pid, creado: new Date().toISOString() }),
      );
      assert.throws(() => blanquearContrasena('chrome'), /no tiene el marcador del harness/);
    });

    test('con el marcador pero sin la base de la instancia, no blanquea', () => {
      writeFileSync(
        path.join(raiz, ARCHIVO_MARCADOR),
        JSON.stringify({ firma: FIRMA_MARCADOR, repo: REPO, pid: process.pid, creado: new Date().toISOString() }),
      );
      assert.throws(() => blanquearContrasena('chrome'), /No existe la base de la instancia chrome/);
    });
  });
});
