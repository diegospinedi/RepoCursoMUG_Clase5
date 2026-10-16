import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, beforeEach, vi } from 'vitest';
import { TOKEN_XSRF_DE_PRUEBA } from './src/pruebas/constantes';
import { verificarPedidosSinRespuesta } from './src/pruebas/servidorFalso';

function borrarCookies() {
  for (const cookie of document.cookie.split(';')) {
    const nombre = cookie.split('=')[0]?.trim();
    if (nombre) {
      document.cookie = `${nombre}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/`;
    }
  }
}

beforeEach(() => {
  // El servidor emite esta cookie (legible por JS) en GET /api/acceso/ingreso.
  document.cookie = `XSRF-TOKEN=${TOKEN_XSRF_DE_PRUEBA}; path=/`;
});

afterEach(() => {
  cleanup();
  verificarPedidosSinRespuesta();
  vi.unstubAllGlobals();
  localStorage.clear();
  sessionStorage.clear();
  borrarCookies();
});
