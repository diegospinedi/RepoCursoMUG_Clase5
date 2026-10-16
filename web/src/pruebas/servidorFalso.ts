import { vi } from 'vitest';

/** Respuesta simulada de la API, o la red caída (fetch rechaza). */
export type RespuestaFalsa = { estado: number; cuerpo?: unknown } | { redCaida: true };

export interface PedidoRegistrado {
  metodo: string;
  ruta: string;
  cuerpo: unknown;
  textoCuerpo: string | null;
  headers: Headers;
  credentials: RequestCredentials | undefined;
}

type Manejador = RespuestaFalsa | RespuestaFalsa[] | ((pedido: PedidoRegistrado) => RespuestaFalsa);

// Pedidos a rutas sin respuesta simulada en el test en curso. El cliente traduce un fetch rechazado a "sin
// conexión", así que el error del fetch solo no alcanza: el setup verifica esta lista después de cada test.
const pedidosSinRespuesta: string[] = [];

/** Falla el test si hubo pedidos a rutas sin respuesta simulada. La llama `vitest.setup.ts` en `afterEach`. */
export function verificarPedidosSinRespuesta(): void {
  const pendientes = pedidosSinRespuesta.splice(0);
  if (pendientes.length > 0) {
    throw new Error(`Pedidos sin respuesta simulada en el servidor falso: ${pendientes.join(', ')}`);
  }
}

/**
 * Reemplaza `fetch` por un servidor en memoria. Las claves son "MÉTODO /ruta". Un arreglo de respuestas se
 * consume en orden y la última se repite.
 */
export function instalarServidorFalso(rutas: Record<string, Manejador>) {
  const pedidos: PedidoRegistrado[] = [];
  const consumidas = new Map<string, number>();

  const fetchFalso = vi.fn(async (entrada: RequestInfo | URL, init?: RequestInit) => {
    const url = new URL(typeof entrada === 'string' ? entrada : entrada.toString(), 'http://localhost');
    const metodo = (init?.method ?? 'GET').toUpperCase();
    const textoCuerpo = typeof init?.body === 'string' ? init.body : null;
    const pedido: PedidoRegistrado = {
      metodo,
      ruta: url.pathname,
      cuerpo: textoCuerpo === null ? null : JSON.parse(textoCuerpo),
      textoCuerpo,
      headers: new Headers(init?.headers),
      credentials: init?.credentials,
    };
    pedidos.push(pedido);

    const clave = `${metodo} ${url.pathname}`;
    const manejador = rutas[clave];
    let respuesta: RespuestaFalsa;
    if (manejador === undefined) {
      pedidosSinRespuesta.push(clave);
      throw new Error(`Sin respuesta simulada para ${clave}`);
    }

    if (typeof manejador === 'function') {
      respuesta = manejador(pedido);
    } else if (Array.isArray(manejador)) {
      const indice = consumidas.get(clave) ?? 0;
      consumidas.set(clave, indice + 1);
      respuesta = manejador[Math.min(indice, manejador.length - 1)]!;
    } else {
      respuesta = manejador;
    }

    if ('redCaida' in respuesta) {
      throw new TypeError('Failed to fetch');
    }

    if (respuesta.cuerpo === undefined) {
      return new Response(null, { status: respuesta.estado });
    }

    return new Response(JSON.stringify(respuesta.cuerpo), {
      status: respuesta.estado,
      headers: { 'Content-Type': respuesta.estado >= 400 ? 'application/problem+json' : 'application/json' },
    });
  });

  vi.stubGlobal('fetch', fetchFalso);

  return {
    pedidos,
    pedidosA: (metodo: string, ruta: string) => pedidos.filter((p) => p.metodo === metodo && p.ruta === ruta),
  };
}

/** 400 de validación con el formato de la API (`ProblemDetails.errors`). */
export function errorDeValidacion(errores: Record<string, string[]>): RespuestaFalsa {
  return { estado: 400, cuerpo: { title: 'Revisá los datos ingresados', status: 400, errors: errores } };
}
