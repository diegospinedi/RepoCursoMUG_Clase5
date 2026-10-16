import { colores } from '@marca/branding.json';
import logo from '@marca/logo.png';
import { screen } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { TOKEN_XSRF_DE_PRUEBA } from '../pruebas/constantes';
import { renderizarConRutas } from '../pruebas/rutas';
import { bloquearLecturaDeCookies } from '../pruebas/cookies';
import { errorDeValidacion, instalarServidorFalso, type RespuestaFalsa } from '../pruebas/servidorFalso';
import { CrearContrasena } from './CrearContrasena';
import { Ingreso } from './Ingreso';

function renderizarCreacion() {
  return renderizarConRutas('/crear-contrasena', [
    ['/crear-contrasena', <CrearContrasena />],
    ['/ingreso', <Ingreso />],
  ]);
}

const SIN_CONEXION = 'No se pudo conectar con el sistema. Intentá de nuevo.';

async function crear(usuario: UserEvent, contrasena: string, confirmacion: string) {
  await usuario.type(screen.getByLabelText('Contraseña'), contrasena);
  await usuario.type(screen.getByLabelText('Repetí la contraseña'), confirmacion);
  await usuario.click(screen.getByRole('button', { name: 'Crear contraseña' }));
}

describe('CrearContrasena', () => {
  it('CrearContrasena_muestraMensajeJuntoAlCampo', async () => {
    const servidor = instalarServidorFalso({
      'POST /api/acceso/contrasena-inicial': errorDeValidacion({
        contrasena: ['La contraseña debe tener al menos 8 caracteres'],
      }),
    });
    renderizarCreacion();

    await crear(userEvent.setup(), 'corta', 'corta');

    const campo = screen.getByLabelText('Contraseña');
    expect(await screen.findByText('La contraseña debe tener al menos 8 caracteres')).toBeInTheDocument();
    expect(campo).toHaveAccessibleDescription('La contraseña debe tener al menos 8 caracteres');
    expect(campo).toHaveAttribute('aria-invalid', 'true');
    const [pedido] = servidor.pedidosA('POST', '/api/acceso/contrasena-inicial');
    expect(pedido?.cuerpo).toEqual({ contrasena: 'corta' });
    expect(pedido?.headers.get('X-XSRF-TOKEN')).toBe(TOKEN_XSRF_DE_PRUEBA);
    expect(pedido?.credentials).toBe('same-origin');
  });

  it('CrearContrasena_confirmacionDistinta_noEnvia', async () => {
    const servidor = instalarServidorFalso({
      'POST /api/acceso/contrasena-inicial': { estado: 204 },
    });
    renderizarCreacion();

    await crear(userEvent.setup(), 'una-contrasena-larga', 'otra-contrasena-larga');

    expect(await screen.findByText('Las contraseñas no coinciden')).toBeInTheDocument();
    expect(screen.getByLabelText('Repetí la contraseña')).toHaveAccessibleDescription('Las contraseñas no coinciden');
    expect(servidor.pedidosA('POST', '/api/acceso/contrasena-inicial')).toHaveLength(0);
  });

  it('CrearContrasena_409_vaAIngresoConAviso', async () => {
    instalarServidorFalso({
      'POST /api/acceso/contrasena-inicial': {
        estado: 409,
        cuerpo: { title: 'La contraseña ya está definida', status: 409 },
      },
    });
    renderizarCreacion();

    await crear(userEvent.setup(), 'una-contrasena-larga', 'una-contrasena-larga');

    expect(await screen.findByRole('heading', { name: 'Ingresar' })).toBeInTheDocument();
    expect(screen.getByText('La contraseña ya estaba definida')).toBeInTheDocument();
  });

  it('CrearContrasena_muestraLogoYColores', () => {
    // Sin colores previos: los tiene que publicar esta pantalla, no un test anterior.
    document.documentElement.removeAttribute('style');
    const { container } = renderizarCreacion();

    const imagen = screen.getByRole('img', { name: 'Óptica Sistema' });
    expect(imagen).toHaveAttribute('src', logo);
    expect(screen.getByRole('banner')).toContainElement(imagen);
    expect(screen.getByRole('heading', { name: 'Crear la contraseña' })).toBeInTheDocument();
    const raiz = document.documentElement.style;
    expect(raiz.getPropertyValue('--color-primario')).toBe(colores.primario);
    expect(raiz.getPropertyValue('--color-fondo')).toBe(colores.fondo);
    expect(container.querySelectorAll('[style]')).toHaveLength(0);
  });

  it.each([
    [
      '400 sin el campo contraseña',
      errorDeValidacion({ otroCampo: ['No es válido'] }),
      'Revisá los datos ingresados',
    ],
    ['429', { estado: 429, cuerpo: { title: 'Demasiados intentos', status: 429 } } as RespuestaFalsa, 'Demasiados intentos'],
    ['red caída', { redCaida: true } as RespuestaFalsa, SIN_CONEXION],
    ['500', { estado: 500, cuerpo: { title: 'Ocurrió un error inesperado', status: 500 } } as RespuestaFalsa, SIN_CONEXION],
  ])('CrearContrasena_%s_muestraMensajeGeneral', async (_caso, respuesta, esperado) => {
    instalarServidorFalso({ 'POST /api/acceso/contrasena-inicial': respuesta });
    renderizarCreacion();

    await crear(userEvent.setup(), 'una-contrasena-larga', 'una-contrasena-larga');

    expect((await screen.findByRole('alert')).textContent).toBe(esperado);
    expect(screen.getByLabelText('Contraseña')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByRole('heading', { name: 'Crear la contraseña' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Crear contraseña' })).toBeEnabled();
  });

  it('CrearContrasena_errorDesconocido_muestraSinConexion', async () => {
    const servidor = instalarServidorFalso({ 'POST /api/acceso/contrasena-inicial': { estado: 204 } });
    renderizarCreacion();
    const usuario = userEvent.setup();
    await usuario.type(screen.getByLabelText('Contraseña'), 'una-contrasena-larga');
    await usuario.type(screen.getByLabelText('Repetí la contraseña'), 'una-contrasena-larga');

    const restaurar = bloquearLecturaDeCookies();
    try {
      await usuario.click(screen.getByRole('button', { name: 'Crear contraseña' }));

      expect((await screen.findByRole('alert')).textContent).toBe(SIN_CONEXION);
      expect(servidor.pedidos).toHaveLength(0);
    } finally {
      restaurar();
    }
  });
});
