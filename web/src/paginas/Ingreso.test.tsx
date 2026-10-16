import logo from '@marca/logo.png';
import { screen } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, it } from 'vitest';
import { TOKEN_XSRF_DE_PRUEBA } from '../pruebas/constantes';
import { renderizarConRutas } from '../pruebas/rutas';
import { instalarServidorFalso } from '../pruebas/servidorFalso';
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
});
