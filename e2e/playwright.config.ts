import { randomBytes } from 'node:crypto';
import { defineConfig, devices, type Project } from '@playwright/test';
import { nuevaRaiz, validarRaiz, VARIABLE_RAIZ } from './scripts/entorno-e2e.mjs';

/**
 * Pruebas de punta a punta de Óptica Sistema (NFR-04, AC-18). Instrucciones: docs/operacion/pruebas-e2e.md.
 *
 * - Proyectos `chrome` y `msedge`: usan el Chrome y el Edge INSTALADOS en la PC (canales de Playwright), no
 *   navegadores descargados. Si falta uno, Playwright falla con un mensaje explícito.
 * - Cada proyecto tiene su propia instancia de la API, con su propia base, en un puerto exclusivo de E2E
 *   (5180 en adelante; nunca el 5080 del sistema real): los navegadores no se pisan.
 * - Cada archivo de tests prepara su propio estado en `beforeAll` (blanqueo y, si hace falta, creación de la
 *   contraseña por API): puede correr solo y no depende del resultado del otro.
 * - Las instancias las publica y levanta scripts/servidor-e2e.mjs, en un directorio temporal nuevo por corrida
 *   (os.tmpdir()/optica-e2e-XXXXXX, fuera del repo). Nunca se reutiliza un servidor existente.
 * - Limpieza: en Linux y macOS el script borra el directorio al terminar. En Windows Playwright cierra el proceso
 *   a la fuerza y el directorio queda hasta que lo borra la corrida siguiente (o se borra a mano).
 * - `OPTICA_E2E_EJECUTABLE_CHROME` / `OPTICA_E2E_EJECUTABLE_EDGE`: ruta a un ejecutable puntual en lugar del canal
 *   instalado (para correr la suite con la versión anterior del navegador).
 * - `OPTICA_E2E_CHROMIUM=1` agrega el proyecto auxiliar `chromium` (navegador descargado con
 *   `npx playwright install chromium`). Solo sirve para validar la suite donde no hay Chrome ni Edge (por ejemplo,
 *   Linux/WSL); no reemplaza la corrida oficial en `chrome` y `msedge`.
 */

interface Instancia {
  proyecto: string;
  puerto: number;
  navegador: Project['use'];
}

/**
 * Navegador de un proyecto: el canal instalado o, si se define la variable de entorno, un ejecutable puntual (por
 * ejemplo, la versión anterior de Chrome o de Edge para la prueba de NFR-04).
 */
function navegador(dispositivo: string, canal: 'chrome' | 'msedge', variable: string): Project['use'] {
  const ejecutable = process.env[variable];
  return ejecutable
    ? { ...devices[dispositivo], launchOptions: { executablePath: ejecutable } }
    : { ...devices[dispositivo], channel: canal };
}

const instancias: Instancia[] = [
  { proyecto: 'chrome', puerto: 5180, navegador: navegador('Desktop Chrome', 'chrome', 'OPTICA_E2E_EJECUTABLE_CHROME') },
  { proyecto: 'msedge', puerto: 5181, navegador: navegador('Desktop Edge', 'msedge', 'OPTICA_E2E_EJECUTABLE_EDGE') },
];

if (process.env.OPTICA_E2E_CHROMIUM === '1') {
  instancias.push({ proyecto: 'chromium', puerto: 5182, navegador: { ...devices['Desktop Chrome'] } });
}

// Raíz de la corrida: se elige acá (sin crearla) y la heredan el servidor y los workers por la variable de entorno.
// La crea scripts/servidor-e2e.mjs, que exige que no exista.
process.env[VARIABLE_RAIZ] ??= nuevaRaiz();
validarRaiz(process.env[VARIABLE_RAIZ]);

// Token de la señal de "listo": el script lo escribe solo después de verificar todas las instancias. Es aleatorio
// por corrida, así ninguna otra salida puede imitarlo.
process.env.OPTICA_E2E_TOKEN_LISTO ??= randomBytes(16).toString('hex');
const token = process.env.OPTICA_E2E_TOKEN_LISTO;

const argumentosServidor = instancias.map(({ proyecto, puerto }) => `${proyecto}:${puerto}`).join(' ');

export default defineConfig({
  testDir: './tests',
  // Dentro de cada archivo los tests son un flujo ordenado (test.describe.serial) sobre la base del proyecto, que
  // comparten los dos archivos: un solo worker, para que nunca corran a la vez sobre la misma base.
  fullyParallel: false,
  workers: 1,
  // Reintentar un paso del flujo no tiene sentido: la base ya cambió.
  retries: 0,
  forbidOnly: !!process.env.CI,
  timeout: 30_000,
  reporter: [['list'], ['html', { open: 'never' }]],
  use: {
    locale: 'es-AR',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
  },
  projects: instancias.map(({ proyecto, puerto, navegador }) => ({
    name: proyecto,
    use: {
      ...navegador,
      // 127.0.0.1 está en AllowedHosts (appsettings.json) y es la única interfaz en la que escucha la API.
      baseURL: `http://127.0.0.1:${puerto}`,
    },
  })),
  webServer: {
    command: `node scripts/servidor-e2e.mjs ${argumentosServidor}`,
    // Sin `url` ni `port`: Playwright no sondea ningún puerto (una instancia ajena podría responder primero). Espera
    // solo la señal que el script escribe después de comprobar que cada puerto lo tiene la instancia propia.
    wait: { stdout: new RegExp(`^OPTICA-E2E-LISTO ${token}$`, 'm') },
    env: { [VARIABLE_RAIZ]: process.env[VARIABLE_RAIZ]!, OPTICA_E2E_TOKEN_LISTO: token },
    // Nunca usar una instancia que ya esté corriendo (podría ser la real, con datos de la óptica). El script
    // controla todos los puertos antes de publicar y antes de cada arranque, y se niega si alguno está ocupado.
    reuseExistingServer: false,
    // dotnet publish corre `npm ci` y `npm run build` del SPA: la primera vez puede tardar.
    timeout: 600_000,
    stdout: 'pipe',
    stderr: 'pipe',
    // En Linux y macOS el script recibe SIGTERM, detiene las instancias y borra el directorio temporal. En Windows
    // Playwright no envía señales (mata el árbol de procesos): el directorio queda y lo borra la corrida siguiente,
    // solo si tiene el marcador del harness de este repo y su servidor ya no corre.
    gracefulShutdown: { signal: 'SIGTERM', timeout: 20_000 },
  },
});
