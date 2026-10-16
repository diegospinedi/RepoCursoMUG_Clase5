import { colores } from '@marca/branding.json';
import logo from '@marca/logo.png';

/**
 * Marca de Óptica Sistema (FR-16). La única fuente de los colores y del logo es `Marca/` en la raíz del repo:
 * acá no se escribe ningún color a mano.
 *
 * Los colores se publican como variables CSS (`--color-primario`, `--color-fondo`) con la API CSSOM
 * (`style.setProperty`), que la política `default-src 'self'` permite: lo que bloquea un CSP sin
 * `'unsafe-inline'` son los atributos `style` del HTML y los elementos `<style>`, no la manipulación de
 * estilos desde un script propio. `marca.css` solo usa las variables.
 */

const COLOR_HEXADECIMAL = /^#[0-9a-fA-F]{6}$/;

function validarColor(valor: unknown, nombre: string): string {
  // Solo se acepta #RRGGBB: un valor arbitrario de branding.json no puede inyectar CSS.
  if (typeof valor !== 'string' || !COLOR_HEXADECIMAL.test(valor)) {
    throw new Error(`Marca/branding.json: el color "${nombre}" tiene que tener el formato #RRGGBB.`);
  }

  return valor;
}

export const marca = Object.freeze({
  nombre: 'Óptica Sistema',
  logo,
  colores: Object.freeze({
    primario: validarColor(colores.primario, 'primario'),
    fondo: validarColor(colores.fondo, 'fondo'),
  }),
});

/** Publica los colores de la marca como variables CSS sobre `raiz` (por defecto, el elemento `<html>`). */
export function aplicarMarca(raiz: HTMLElement = document.documentElement): void {
  raiz.style.setProperty('--color-primario', marca.colores.primario);
  raiz.style.setProperty('--color-fondo', marca.colores.fondo);
}
