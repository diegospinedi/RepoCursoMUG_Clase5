import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, describe, expect, it, vi, type MockInstance } from 'vitest';
import { Configuracion } from './paginas/Configuracion';
import { CrearContrasena } from './paginas/CrearContrasena';
import { Ingreso } from './paginas/Ingreso';
import { renderizarConRutas } from './pruebas/rutas';
import { instalarServidorFalso } from './pruebas/servidorFalso';

const SECRETO = 'Secreta-Unica-7319';

// Espías nuevos en cada test; la configuración `restoreMocks` los restaura al terminar.
let setItem: MockInstance;
let consola: MockInstance[];

beforeEach(() => {
  setItem = vi.spyOn(Storage.prototype, 'setItem');
  consola = (['log', 'info', 'warn', 'error', 'debug'] as const).map((metodo) =>
    vi.spyOn(console, metodo).mockImplementation(() => undefined),
  );
});

function verificarSinFugas() {
  expect(setItem).not.toHaveBeenCalled();
  expect(localStorage.length).toBe(0);
  expect(sessionStorage.length).toBe(0);
  expect(window.location.href).not.toContain(SECRETO);
  expect(document.cookie).not.toContain(SECRETO);
  expect(document.body.textContent).not.toContain(SECRETO);
  for (const espia of consola) {
    expect(JSON.stringify(espia.mock.calls)).not.toContain(SECRETO);
  }
  // El formulario se limpia después de cada envío.
  for (const campo of document.querySelectorAll<HTMLInputElement>('input[type="password"]')) {
    expect(campo.value).toBe('');
  }
}

describe('Contrasena_noSeGuardaEnStorage', () => {
  it('en el ingreso', async () => {
    const usuario = userEvent.setup();
    const servidor = instalarServidorFalso({
      'POST /api/acceso/ingreso': {
        estado: 401,
        cuerpo: { title: 'La contraseña no es correcta', status: 401, intentosRestantes: 4 },
      },
    });
    renderizarConRutas('/ingreso', [['/ingreso', <Ingreso />]]);

    await usuario.type(screen.getByLabelText('Contraseña'), SECRETO);
    await usuario.click(screen.getByRole('button', { name: 'Ingresar' }));

    await screen.findByText('La contraseña no es correcta. Te quedan 4 intentos.');
    expect(servidor.pedidosA('POST', '/api/acceso/ingreso')[0]?.cuerpo).toEqual({ contrasena: SECRETO });
    verificarSinFugas();
  });

  it('en la creación inicial', async () => {
    const usuario = userEvent.setup();
    const servidor = instalarServidorFalso({ 'POST /api/acceso/contrasena-inicial': { estado: 204 } });
    renderizarConRutas('/crear-contrasena', [
      ['/crear-contrasena', <CrearContrasena />],
      ['/ingreso', <Ingreso />],
    ]);

    await usuario.type(screen.getByLabelText('Contraseña'), SECRETO);
    await usuario.type(screen.getByLabelText('Repetí la contraseña'), SECRETO);
    await usuario.click(screen.getByRole('button', { name: 'Crear contraseña' }));

    await screen.findByRole('heading', { name: 'Ingresar' });
    expect(servidor.pedidosA('POST', '/api/acceso/contrasena-inicial')[0]?.cuerpo).toEqual({ contrasena: SECRETO });
    verificarSinFugas();
  });

  it('en el cambio de contraseña', async () => {
    const usuario = userEvent.setup();
    const servidor = instalarServidorFalso({
      'GET /api/configuracion': {
        estado: 200,
        cuerpo: { alicuotaIva: 21, condicionFiscal: 'Monotributo', topeIdentificacion: null, multiploRedondeo: 1 },
      },
      'PUT /api/acceso/contrasena': { estado: 204 },
    });
    renderizarConRutas('/configuracion', [['/configuracion', <Configuracion />]]);
    await waitFor(() => expect(screen.getByLabelText('Múltiplo de redondeo')).toHaveValue('1'));

    await usuario.type(screen.getByLabelText('Contraseña actual'), SECRETO);
    await usuario.type(screen.getByLabelText('Contraseña nueva'), `${SECRETO}-nueva`);
    await usuario.type(screen.getByLabelText('Repetí la contraseña nueva'), `${SECRETO}-nueva`);
    await usuario.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));

    await screen.findByText('La contraseña se cambió');
    expect(servidor.pedidosA('PUT', '/api/acceso/contrasena')[0]?.cuerpo).toEqual({
      contrasenaActual: SECRETO,
      contrasenaNueva: `${SECRETO}-nueva`,
    });
    verificarSinFugas();
  });
});
