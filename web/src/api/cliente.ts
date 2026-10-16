/**
 * Cliente HTTP de la API de Óptica Sistema.
 *
 * - Usa `fetch` con `credentials: 'same-origin'`: la cookie de sesión (`HttpOnly`, `SameSite=Strict`) viaja sola.
 * - En todo pedido que no es GET manda el header `X-XSRF-TOKEN` con el valor de la cookie `XSRF-TOKEN`, que el
 *   servidor emite en `GET /api/acceso/ingreso` y reemite después del ingreso, del cambio de contraseña y de la
 *   salida. Se lee en cada pedido, así siempre se usa el último.
 * - Traduce `ProblemDetails.errors` a un mensaje por campo (FR-17).
 * - Un 401 en un pedido autenticado avisa con el evento {@link EVENTO_SESION_VENCIDA}, y la aplicación vuelve a
 *   la pantalla de ingreso (E5-2).
 * - Un 5xx o la red caída se informan con un mensaje fijo (E5-6).
 *
 * Nunca guarda nada en `localStorage`/`sessionStorage`, nunca pone datos en la URL y nunca escribe en la
 * consola: las contraseñas solo viajan en el cuerpo del pedido.
 */

export const EVENTO_SESION_VENCIDA = 'optica:sesion-vencida';
export const MENSAJE_SIN_CONEXION = 'No se pudo conectar con el sistema. Intentá de nuevo.';
export const MENSAJE_SESION_VENCIDA = 'Tu sesión venció. Ingresá de nuevo.';
/** Largo máximo de una contraseña que acepta la API (FR-04). */
export const MAXIMO_CONTRASENA = 128;

const COOKIE_XSRF = 'XSRF-TOKEN';
const HEADER_XSRF = 'X-XSRF-TOKEN';

export type CondicionFiscal = 'ResponsableInscripto' | 'Monotributo';

export interface EstadoAcceso {
  contrasenaDefinida: boolean;
  bloqueadoHasta: string | null;
  sesionActiva: boolean;
}

export interface ConfiguracionNegocio {
  alicuotaIva: number;
  condicionFiscal: CondicionFiscal;
  topeIdentificacion: number | null;
  multiploRedondeo: number;
}

/** Respuesta de error de la API (4xx) con su título, los mensajes por campo y los datos adicionales. */
export class ErrorApi extends Error {
  constructor(
    readonly estado: number,
    readonly titulo: string,
    readonly campos: Readonly<Record<string, string>>,
    readonly extensiones: Readonly<Record<string, unknown>>,
  ) {
    super(titulo);
    this.name = 'ErrorApi';
  }
}

/** La API no respondió (red caída) o respondió con un 5xx. */
export class ErrorConexion extends Error {
  constructor() {
    super(MENSAJE_SIN_CONEXION);
    this.name = 'ErrorConexion';
  }
}

/** Un pedido autenticado volvió 401: la aplicación ya fue avisada con {@link EVENTO_SESION_VENCIDA}. */
export class ErrorSesionVencida extends Error {
  constructor() {
    super(MENSAJE_SESION_VENCIDA);
    this.name = 'ErrorSesionVencida';
  }
}

type Metodo = 'GET' | 'POST' | 'PUT';

interface OpcionesPedido {
  metodo?: Metodo;
  cuerpo?: unknown;
  /** El endpoint exige sesión: un 401 significa que la sesión venció. */
  conSesion: boolean;
}

function leerCookie(nombre: string): string | null {
  for (const parte of document.cookie.split(';')) {
    const separador = parte.indexOf('=');
    if (separador > 0 && parte.slice(0, separador).trim() === nombre) {
      try {
        return decodeURIComponent(parte.slice(separador + 1).trim());
      } catch {
        return null;
      }
    }
  }

  return null;
}

async function tokenXsrf(): Promise<string | null> {
  const token = leerCookie(COOKIE_XSRF);
  if (token !== null) {
    return token;
  }

  // Sin cookie (por ejemplo, la borró el navegador): el estado del acceso la vuelve a emitir.
  await obtenerEstadoAcceso();
  return leerCookie(COOKIE_XSRF);
}

async function leerJson(respuesta: Response): Promise<unknown> {
  try {
    return await respuesta.json();
  } catch {
    return null;
  }
}

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

async function errorDesde(respuesta: Response): Promise<ErrorApi> {
  const cuerpo = await leerJson(respuesta);
  const problema = esObjeto(cuerpo) ? cuerpo : {};
  const { title, status: _estado, errors, type: _tipo, traceId: _traza, ...extensiones } = problema;

  const campos: Record<string, string> = {};
  if (esObjeto(errors)) {
    for (const [campo, mensajes] of Object.entries(errors)) {
      const textos = Array.isArray(mensajes) ? mensajes.filter((m): m is string => typeof m === 'string') : [];
      if (textos.length > 0) {
        campos[campo] = textos.join(' ');
      }
    }
  }

  return new ErrorApi(respuesta.status, typeof title === 'string' ? title : 'El pedido no es válido', campos, extensiones);
}

async function pedir(ruta: string, { metodo = 'GET', cuerpo, conSesion }: OpcionesPedido): Promise<Response> {
  const headers: Record<string, string> = { Accept: 'application/json, application/problem+json' };
  if (cuerpo !== undefined) {
    headers['Content-Type'] = 'application/json';
  }

  if (metodo !== 'GET') {
    const token = await tokenXsrf();
    if (token !== null) {
      headers[HEADER_XSRF] = token;
    }
  }

  let respuesta: Response;
  try {
    respuesta = await fetch(ruta, {
      method: metodo,
      headers,
      credentials: 'same-origin',
      cache: 'no-store',
      body: cuerpo === undefined ? null : JSON.stringify(cuerpo),
    });
  } catch {
    throw new ErrorConexion();
  }

  if (respuesta.ok) {
    return respuesta;
  }

  if (respuesta.status === 401 && conSesion) {
    window.dispatchEvent(new Event(EVENTO_SESION_VENCIDA));
    throw new ErrorSesionVencida();
  }

  if (respuesta.status >= 500) {
    throw new ErrorConexion();
  }

  throw await errorDesde(respuesta);
}

async function pedirJson<T>(ruta: string, opciones: OpcionesPedido): Promise<T> {
  const respuesta = await pedir(ruta, opciones);
  const cuerpo = await leerJson(respuesta);
  if (cuerpo === null) {
    throw new ErrorConexion();
  }

  return cuerpo as T;
}

/** `GET /api/acceso/ingreso`: estado del acceso; además emite la cookie `XSRF-TOKEN`. */
export function obtenerEstadoAcceso(): Promise<EstadoAcceso> {
  return pedirJson<EstadoAcceso>('/api/acceso/ingreso', { conSesion: false });
}

/** `POST /api/acceso/contrasena-inicial`: crea la contraseña cuando todavía no existe. */
export async function crearContrasenaInicial(contrasena: string): Promise<void> {
  await pedir('/api/acceso/contrasena-inicial', { metodo: 'POST', cuerpo: { contrasena }, conSesion: false });
}

/** `POST /api/acceso/ingreso`: un 401 acá es una contraseña incorrecta, no una sesión vencida. */
export async function ingresar(contrasena: string): Promise<void> {
  await pedir('/api/acceso/ingreso', { metodo: 'POST', cuerpo: { contrasena }, conSesion: false });
}

/** `POST /api/acceso/salida`. */
export async function salir(): Promise<void> {
  await pedir('/api/acceso/salida', { metodo: 'POST', conSesion: true });
}

/** `PUT /api/acceso/contrasena`. */
export async function cambiarContrasena(contrasenaActual: string, contrasenaNueva: string): Promise<void> {
  await pedir('/api/acceso/contrasena', {
    metodo: 'PUT',
    cuerpo: { contrasenaActual, contrasenaNueva },
    conSesion: true,
  });
}

/** `GET /api/configuracion`. */
export function obtenerConfiguracion(): Promise<ConfiguracionNegocio> {
  return pedirJson<ConfiguracionNegocio>('/api/configuracion', { conSesion: true });
}

/** `PUT /api/configuracion`: devuelve la configuración guardada. */
export function guardarConfiguracion(configuracion: ConfiguracionNegocio): Promise<ConfiguracionNegocio> {
  return pedirJson<ConfiguracionNegocio>('/api/configuracion', {
    metodo: 'PUT',
    cuerpo: configuracion,
    conSesion: true,
  });
}
