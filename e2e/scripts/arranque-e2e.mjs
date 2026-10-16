// Arranque y verificación de identidad de una instancia de la API para las pruebas de punta a punta.
// Las dependencias (proceso, consulta HTTP, sistema de archivos, control de puerto) se inyectan para poder probar
// cada regla sin levantar la API (scripts/servidor-e2e.test.mjs).

import { existsSync } from 'node:fs';
import path from 'node:path';
import { createInterface } from 'node:readline';

/** Únicos puertos que usa E2E. El 5080 (sistema real) y cualquier otro se rechazan. */
export const PUERTOS_E2E = new Set([5180, 5181, 5182]);

export function validarPuerto(puerto) {
  if (!PUERTOS_E2E.has(puerto)) {
    throw new Error(
      `el puerto ${puerto} no es de E2E (se permiten ${[...PUERTOS_E2E].join(', ')}). ` +
        'El 5080 es el del sistema real y nunca se usa en las pruebas.',
    );
  }

  return puerto;
}

/** Línea exacta que escribe Microsoft.Hosting.Lifetime cuando Kestrel abre el puerto (con la sangría del log). */
export function patronEscucha(puerto) {
  return new RegExp(`^\\s*Now listening on: http://127\\.0\\.0\\.1:${puerto}$`);
}

export function mensajeOcupado(puerto) {
  return (
    `el puerto ${puerto} de 127.0.0.1 ya está en uso por otro proceso. Las pruebas nunca usan una instancia ` +
    'existente: liberá el puerto y volvé a correrlas.'
  );
}

/** Estado del acceso que informa el puerto, o null si no responde 200 con JSON. */
export async function consultarEstado(puerto) {
  try {
    const respuesta = await fetch(`http://127.0.0.1:${puerto}/api/acceso/ingreso`, {
      signal: AbortSignal.timeout(2000),
    });
    return respuesta.ok ? await respuesta.json() : null;
  } catch {
    return null;
  }
}

/** Control inmediatamente anterior al arranque: si el puerto ya está ocupado, no se lanza nada. */
export async function lanzarInstancia({ puerto, puertoOcupado, lanzar }) {
  validarPuerto(puerto);
  if (await puertoOcupado(puerto)) {
    throw new Error(mensajeOcupado(puerto));
  }

  return lanzar();
}

const esperar = (ms) => new Promise((resolver) => setTimeout(resolver, ms));

function termino(proceso) {
  return proceso.exitCode !== null || (proceso.signalCode ?? null) !== null;
}

/**
 * Espera a que la instancia arranque y comprueba que quien responde en el puerto es ELLA y no otro proceso. Lanza
 * un error si no se cumple todo:
 * 1. el proceso hijo escribió exactamente `Now listening on: http://127.0.0.1:<puerto>`;
 * 2. el puerto responde y la base está vacía (sin contraseña, sin sesión);
 * 3. la base recién creada existe en SU directorio de datos;
 * 4. pasada la ventana de estabilidad, el proceso sigue vivo y el puerto sigue respondiendo.
 */
export async function esperarArranque({
  nombre,
  puerto,
  proceso,
  datos,
  consultar = consultarEstado,
  existe = existsSync,
  plazoMs = 60_000,
  estabilidadMs = 750,
  intervaloMs = 250,
  alLeer = () => {},
}) {
  const escucha = patronEscucha(puerto);
  let escuchando = false;
  createInterface({ input: proceso.stdout }).on('line', (linea) => {
    if (escucha.test(linea)) {
      escuchando = true;
    }

    alLeer(linea);
  });

  const limite = Date.now() + plazoMs;
  while (Date.now() < limite) {
    if (termino(proceso)) {
      throw new Error(
        `la instancia ${nombre} terminó al arrancar (código ${proceso.exitCode}, señal ${proceso.signalCode ?? null}).`,
      );
    }

    const estado = escuchando ? await consultar(puerto) : null;
    if (estado) {
      if (estado.contrasenaDefinida !== false || estado.sesionActiva !== false) {
        throw new Error(`en el puerto ${puerto} responde un servidor con datos: no es la instancia nueva.`);
      }

      const base = path.join(datos, 'base', 'optica.db');
      if (!existe(base)) {
        throw new Error(`la instancia ${nombre} no creó su base en ${base}: no es la que responde.`);
      }

      await esperar(estabilidadMs);
      if (termino(proceso) || !(await consultar(puerto))) {
        throw new Error(`la instancia ${nombre} no quedó corriendo en el puerto ${puerto}.`);
      }

      return;
    }

    await esperar(intervaloMs);
  }

  throw new Error(`la instancia ${nombre} no respondió como propia en http://127.0.0.1:${puerto}.`);
}
