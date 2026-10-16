import { screen } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { TOKEN_XSRF_DE_PRUEBA } from '../pruebas/constantes';
import { renderizarConRutas } from '../pruebas/rutas';
import { errorDeValidacion, instalarServidorFalso } from '../pruebas/servidorFalso';
import { CrearContrasena } from './CrearContrasena';
import { Ingreso } from './Ingreso';

function renderizarCreacion() {
  return renderizarConRutas('/crear-contrasena', [
    ['/crear-contrasena', <CrearContrasena />],
    ['/ingreso', <Ingreso />],
  ]);
}

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
});
