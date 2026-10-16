import { readFileSync } from 'node:fs';
import { test as base, expect, request as fabricaPedidos, type Page, type Request, type TestInfo } from '@playwright/test';
import { blanquearContrasena } from '../scripts/entorno-e2e.mjs';

/**
 * Soporte común de la suite de punta a punta.
 *
 * Cada archivo prepara su propio estado en `beforeAll` (ver {@link prepararBaseSinContrasena} y
 * {@link prepararBaseConContrasena}): puede correr solo, en cualquier orden, y el resultado de un archivo no
 * afecta al otro.
 *
 * Las contraseñas son ficticias y solo existen en la base temporal de la corrida.
 */
export const CONTRASENA_INICIAL = 'prueba-e2e-inicial-1234';
export const CONTRASENA_NUEVA = 'prueba-e2e-nueva-5678';
export const CONTRASENA_CONFIGURACION = 'prueba-e2e-configuracion-9012';

export const MENSAJE_SESION_VENCIDA = 'Tu sesión venció. Ingresá de nuevo.';
export const COOKIE_SESION = 'optica.sesion';

const marca = JSON.parse(readFileSync(new URL('../../Marca/branding.json', import.meta.url), 'utf8')) as {
  colores: { primario: string; fondo: string };
};

/** Color de Marca/branding.json (#RRGGBB) en el formato que devuelve getComputedStyle: rgb(r, g, b). */
function aRgb(hexadecimal: string): string {
  const [r, g, b] = [1, 3, 5].map((inicio) => Number.parseInt(hexadecimal.slice(inicio, inicio + 2), 16));
  return `rgb(${r}, ${g}, ${b})`;
}

export const COLOR_PRIMARIO = aRgb(marca.colores.primario);
export const COLOR_FONDO = aRgb(marca.colores.fondo);

// --- Preparación por archivo -------------------------------------------------------------------------------

function urlBase(testInfo: TestInfo): string {
  const url = testInfo.project.use.baseURL;
  if (!url) {
    throw new Error(`El proyecto ${testInfo.project.name} no tiene baseURL.`);
  }

  return url;
}

/**
 * Deja la base del proyecto sin contraseña y sin intentos fallidos, con el comando de blanqueo de la API (FR-06),
 * y comprueba por API que quedó así.
 */
export async function prepararBaseSinContrasena(testInfo: TestInfo): Promise<void> {
  blanquearContrasena(testInfo.project.name);

  const pedidos = await fabricaPedidos.newContext({ baseURL: urlBase(testInfo) });
  try {
    const estado = await pedidos.get('/api/acceso/ingreso');
    expect(estado.status()).toBe(200);
    expect(await estado.json()).toMatchObject({ contrasenaDefinida: false, bloqueadoHasta: null, sesionActiva: false });
  } finally {
    await pedidos.dispose();
  }
}

/** Base del proyecto con la contraseña `contrasena`, creada por API después de blanquear. */
export async function prepararBaseConContrasena(testInfo: TestInfo, contrasena: string): Promise<void> {
  await prepararBaseSinContrasena(testInfo);

  const pedidos = await fabricaPedidos.newContext({ baseURL: urlBase(testInfo) });
  try {
    // GET emite la cookie XSRF-TOKEN; el POST la devuelve en el header, como el SPA.
    await pedidos.get('/api/acceso/ingreso');
    const xsrf = (await pedidos.storageState()).cookies.find((cookie) => cookie.name === 'XSRF-TOKEN');
    expect(xsrf, 'cookie XSRF-TOKEN').toBeDefined();

    const creada = await pedidos.post('/api/acceso/contrasena-inicial', {
      data: { contrasena },
      headers: { 'X-XSRF-TOKEN': decodeURIComponent(xsrf!.value) },
    });
    expect(creada.status()).toBe(204);
  } finally {
    await pedidos.dispose();
  }
}

// --- Vigilancia de la consola ------------------------------------------------------------------------------

const RECURSO_FALLIDO = /^Failed to load resource: the server responded with a status of (\d{3})\b/;
const RECURSO_ABORTADO = /^Failed to load resource: net::ERR_ABORTED\b/;

interface ViolacionCsp {
  directiva: string;
  recurso: string;
}

/** Vigilancia de la consola del navegador: errores, excepciones no capturadas y violaciones de CSP. */
export interface Consola {
  /**
   * Declara que en este test la API responde a propósito con `estado` en `ruta` (por ejemplo, 401 en
   * `/api/acceso/ingreso` al ingresar con una contraseña incorrecta). Chrome y Edge registran esas respuestas
   * como error de consola ("Failed to load resource"); solo se toleran las combinaciones declaradas.
   */
  permitirRespuesta(estado: number, ruta: string): void;
}

function rutaDe(url: string): string | null {
  try {
    return new URL(url).pathname;
  } catch {
    return null;
  }
}

/** Da tiempo a que lleguen los eventos pendientes de la página (consola, CSP) antes de comparar. */
async function drenar(page: Page): Promise<void> {
  if (page.isClosed()) {
    return;
  }

  try {
    // Dos vueltas completas de la página: lo que se haya encolado antes llega antes que la respuesta.
    for (let vuelta = 0; vuelta < 2; vuelta++) {
      await page.evaluate(
        () => new Promise<void>((resolver) => requestAnimationFrame(() => setTimeout(resolver, 50))),
      );
    }
  } catch {
    // La página se cerró o está navegando: lo ya recibido es lo que se compara.
  }
}

export const test = base.extend<{ consola: Consola }>({
  consola: [
    async ({ page }, usar) => {
      const errores: string[] = [];
      const abortados: { url: string; texto: string }[] = [];
      const violaciones: ViolacionCsp[] = [];
      const permitidos = new Set<string>();
      // Pedidos cancelados por una navegación de su frame (page.goto, reload): no son errores de la aplicación.
      const navegacionesPorFrame = new Map<unknown, number>();
      // Cada pedido cancelado por navegación justifica a lo sumo UN mensaje ERR_ABORTED de su misma URL.
      const abortadosPorNavegacion: Request[] = [];
      const navegacionAlPedir = new WeakMap<object, number>();

      const frameDe = (pedido: Request): unknown => {
        try {
          return pedido.frame();
        } catch {
          return null; // Pedidos de un service worker: no tienen frame.
        }
      };
      page.on('framenavigated', (frame) => navegacionesPorFrame.set(frame, (navegacionesPorFrame.get(frame) ?? 0) + 1));
      page.on('request', (pedido) => navegacionAlPedir.set(pedido, navegacionesPorFrame.get(frameDe(pedido)) ?? 0));
      page.on('requestfailed', (pedido) => {
        const navegacionesAntes = navegacionAlPedir.get(pedido);
        const navegacionesAhora = navegacionesPorFrame.get(frameDe(pedido)) ?? 0;
        if (
          pedido.failure()?.errorText.includes('ERR_ABORTED') &&
          navegacionesAntes !== undefined &&
          navegacionesAhora > navegacionesAntes
        ) {
          abortadosPorNavegacion.push(pedido);
        }
      });

      page.on('console', (mensaje) => {
        if (mensaje.type() !== 'error') {
          return;
        }

        const texto = mensaje.text();
        const url = mensaje.location().url;
        const recurso = RECURSO_FALLIDO.exec(texto);
        if (recurso && permitidos.has(`${recurso[1]} ${rutaDe(url)}`)) {
          return;
        }

        if (RECURSO_ABORTADO.test(texto)) {
          // Se decide al final: el evento requestfailed puede llegar después del mensaje de consola.
          abortados.push({ url, texto });
          return;
        }

        errores.push(`${texto} (${url})`);
      });
      page.on('pageerror', (error) => errores.push(`Excepción no capturada: ${error.message}`));

      // El evento securitypolicyviolation informa toda violación de CSP, aunque el navegador no la escriba en la
      // consola. La función expuesta sobrevive a las navegaciones; el script de inicio corre en cada documento.
      await page.exposeFunction('__opticaViolacionCsp', (violacion: ViolacionCsp) => {
        violaciones.push(violacion);
      });
      await page.addInitScript(() => {
        document.addEventListener('securitypolicyviolation', (evento) => {
          const reportar = (window as unknown as Record<string, (v: unknown) => void>)['__opticaViolacionCsp'];
          reportar?.({ directiva: evento.violatedDirective, recurso: evento.blockedURI });
        });
      });

      await usar({ permitirRespuesta: (estado, ruta) => permitidos.add(`${estado} ${ruta}`) });

      await drenar(page);
      for (const { url, texto } of abortados) {
        const indice = abortadosPorNavegacion.findIndex((pedido) => pedido.url() === url);
        if (indice === -1) {
          errores.push(`${texto} (${url})`);
        } else {
          abortadosPorNavegacion.splice(indice, 1);
        }
      }

      expect(violaciones, 'violaciones de CSP en el navegador').toEqual([]);
      expect(errores, 'errores en la consola del navegador').toEqual([]);
    },
    { auto: true },
  ],
});

export { expect };

// --- Pasos comunes -----------------------------------------------------------------------------------------

/** Ingresa desde la pantalla de ingreso y espera la pantalla de Configuración con los valores cargados. */
export async function ingresar(page: Page, contrasena: string): Promise<void> {
  await page.goto('/ingreso');
  await expect(page.getByRole('heading', { name: 'Ingresar' })).toBeVisible();
  await page.getByLabel('Contraseña', { exact: true }).fill(contrasena);
  await page.getByRole('button', { name: 'Ingresar' }).click();
  await esperarConfiguracion(page);
}

/** Espera la pantalla de Configuración con el formulario ya cargado desde la API. */
export async function esperarConfiguracion(page: Page): Promise<void> {
  await expect(page).toHaveURL(/\/configuracion$/);
  await expect(page.getByRole('heading', { name: 'Configuración', level: 1 })).toBeVisible();
  await expect(page.getByRole('button', { name: 'Grabar' })).toBeVisible();
}
