import { vi } from 'vitest';

/**
 * Hace que leer `document.cookie` falle, como pasa en un navegador que bloquea las cookies (SecurityError). Así el
 * cliente propaga un error que no es de la API ni de la red. Devuelve la función que restaura la lectura: hay que
 * llamarla antes de terminar el test, porque el setup lee las cookies en `afterEach`.
 */
export function bloquearLecturaDeCookies(): () => void {
  const espia = vi.spyOn(document, 'cookie', 'get').mockImplementation(() => {
    throw new DOMException('Las cookies están bloqueadas', 'SecurityError');
  });
  return () => espia.mockRestore();
}
