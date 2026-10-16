import logo from '@marca/logo.png';
import { screen, waitFor } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { TOKEN_XSRF_DE_PRUEBA } from '../pruebas/constantes';
import { renderizarConRutas } from '../pruebas/rutas';
import { bloquearLecturaDeCookies } from '../pruebas/cookies';
import { errorDeValidacion, instalarServidorFalso, type RespuestaFalsa } from '../pruebas/servidorFalso';
import { Ingreso } from './Ingreso';

function renderizarIngreso() {
  return renderizarConRutas('/ingreso', [
    ['/ingreso', <Ingreso />],
    ['/configuracion', <p>Pantalla de configuración</p>],
    ['/crear-contrasena', <p>Pantalla de creación de contraseña</p>],
  ]);
}

async function ingresar(usuario: UserEvent, contrasena: string) {
  await usuario.type(screen.getByLabelText('Contraseña'), contrasena);
  await usuario.click(screen.getByRole('button', { name: 'Ingresar' }));
}

const SIN_CONEXION = 'No se pudo conectar con el sistema. Intentá de nuevo.';

/** Ingresa una contraseña con el servidor respondiendo `respuesta` y devuelve el mensaje de error que se muestra. */
async function mensajeAnte(respuesta: RespuestaFalsa): Promise<HTMLElement> {
  instalarServidorFalso({ 'POST /api/acceso/ingreso': respuesta });
  renderizarIngreso();
  await ingresar(userEvent.setup(), 'cualquiera');
  return screen.findByRole('alert');
}

describe('Ingreso', () => {
  it('Ingreso_muestraLogo', () => {
    renderizarIngreso();

    expect(screen.getByRole('img', { name: 'Óptica Sistema' })).toHaveAttribute('src', logo);
    expect(screen.getByRole('heading', { name: 'Ingresar' })).toBeInTheDocument();
  });

  it('Ingreso_204_vaAConfiguracion', async () => {
    const servidor = instalarServidorFalso({ 'POST /api/acceso/ingreso': { estado: 204 } });
    renderizarIngreso();

    await ingresar(userEvent.setup(), 'la-correcta-123');

    expect(await screen.findByText('Pantalla de configuración')).toBeInTheDocument();
    const [pedido] = servidor.pedidosA('POST', '/api/acceso/ingreso');
    expect(pedido?.cuerpo).toEqual({ contrasena: 'la-correcta-123' });
    expect(pedido?.headers.get('X-XSRF-TOKEN')).toBe(TOKEN_XSRF_DE_PRUEBA);
    expect(pedido?.credentials).toBe('same-origin');
  });

  it('Ingreso_401_muestraIntentosRestantes', async () => {
    instalarServidorFalso({
      'POST /api/acceso/ingreso': {
        estado: 401,
        cuerpo: { title: 'La contraseña no es correcta', status: 401, intentosRestantes: 3 },
      },
    });
    renderizarIngreso();

    await ingresar(userEvent.setup(), 'no-es-esta');

    expect(await screen.findByText('La contraseña no es correcta. Te quedan 3 intentos.')).toBeInTheDocument();
    expect(screen.getByLabelText('Contraseña')).toHaveValue('');
    expect(screen.queryByText('Pantalla de configuración')).not.toBeInTheDocument();
  });

  it('Ingreso_423_muestraHoraDeLiberacion', async () => {
    // 14:35 en la hora local del navegador.
    const hasta = new Date(2026, 9, 16, 14, 35, 0);
    instalarServidorFalso({
      'POST /api/acceso/ingreso': {
        estado: 423,
        cuerpo: { title: 'Ingreso bloqueado', status: 423, bloqueadoHasta: hasta.toISOString() },
      },
    });
    renderizarIngreso();

    await ingresar(userEvent.setup(), 'cualquiera');

    expect(await screen.findByText('Ingreso bloqueado hasta 14:35')).toBeInTheDocument();
  });

  it('Ingreso_409_vaACrearContrasena', async () => {
    instalarServidorFalso({
      'POST /api/acceso/ingreso': {
        estado: 409,
        cuerpo: { title: 'Todavía no se creó la contraseña', status: 409 },
      },
    });
    renderizarIngreso();

    await ingresar(userEvent.setup(), 'cualquiera');

    expect(await screen.findByText('Pantalla de creación de contraseña')).toBeInTheDocument();
  });

  it('Ingreso_401_conUnIntento_usaSingular', async () => {
    const alerta = await mensajeAnte({
      estado: 401,
      cuerpo: { title: 'La contraseña no es correcta', status: 401, intentosRestantes: 1 },
    });

    expect(alerta).toHaveTextContent('La contraseña no es correcta. Te queda 1 intento.');
  });

  it('Ingreso_401_sinIntentosRestantes_muestraMensajeGenerico', async () => {
    const alerta = await mensajeAnte({ estado: 401, cuerpo: { title: 'La contraseña no es correcta', status: 401 } });

    expect(alerta.textContent).toBe('La contraseña no es correcta.');
  });

  it.each([
    ['sin bloqueadoHasta', { title: 'Ingreso bloqueado', status: 423 }],
    ['con bloqueadoHasta que no es texto', { title: 'Ingreso bloqueado', status: 423, bloqueadoHasta: 1234 }],
    ['con bloqueadoHasta que no es una fecha', { title: 'Ingreso bloqueado', status: 423, bloqueadoHasta: 'mañana' }],
  ])('Ingreso_423_%s_muestraBloqueoSinHora', async (_caso, cuerpo) => {
    const alerta = await mensajeAnte({ estado: 423, cuerpo });

    expect(alerta.textContent).toBe('Ingreso bloqueado. Intentá más tarde.');
  });

  it('Ingreso_400_conCampoContrasena_muestraMensajeJuntoAlCampo', async () => {
    instalarServidorFalso({
      'POST /api/acceso/ingreso': errorDeValidacion({ contrasena: ['La contraseña es obligatoria'] }),
    });
    renderizarIngreso();

    await ingresar(userEvent.setup(), 'x');

    const campo = screen.getByLabelText('Contraseña');
    await waitFor(() => expect(campo).toHaveAccessibleDescription('La contraseña es obligatoria'));
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
  });

  it.each([
    [400, { title: 'Revisá los datos ingresados', status: 400, errors: { otroCampo: ['No es válido'] } }, 'Revisá los datos ingresados'],
    [429, { title: 'Demasiados intentos', status: 429 }, 'Demasiados intentos'],
    [403, { status: 403 }, 'El pedido no es válido'],
  ])('Ingreso_%d_sinCampoContrasena_muestraElTitulo', async (estado, cuerpo, esperado) => {
    const alerta = await mensajeAnte({ estado, cuerpo });

    expect(alerta.textContent).toBe(esperado);
    expect(screen.getByLabelText('Contraseña')).not.toHaveAttribute('aria-invalid');
  });

  it.each([
    ['red caída', { redCaida: true } as RespuestaFalsa],
    ['500', { estado: 500, cuerpo: { title: 'Ocurrió un error inesperado', status: 500 } }],
  ])('Ingreso_%s_muestraSinConexion', async (_caso, respuesta) => {
    const alerta = await mensajeAnte(respuesta);

    expect(alerta.textContent).toBe(SIN_CONEXION);
    expect(screen.getByRole('button', { name: 'Ingresar' })).toBeEnabled();
  });

  it('Ingreso_errorDesconocido_muestraSinConexion', async () => {
    const servidor = instalarServidorFalso({ 'POST /api/acceso/ingreso': { estado: 204 } });
    renderizarIngreso();
    const usuario = userEvent.setup();
    await usuario.type(screen.getByLabelText('Contraseña'), 'cualquiera');

    const restaurar = bloquearLecturaDeCookies();
    try {
      await usuario.click(screen.getByRole('button', { name: 'Ingresar' }));

      expect((await screen.findByRole('alert')).textContent).toBe(SIN_CONEXION);
      expect(servidor.pedidos).toHaveLength(0);
      expect(screen.queryByText('Pantalla de configuración')).not.toBeInTheDocument();
    } finally {
      restaurar();
    }
  });
});
