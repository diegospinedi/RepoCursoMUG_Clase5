import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { App } from './App';
import { instalarServidorFalso } from './pruebas/servidorFalso';

describe('App', () => {
  it('App_sinContrasena_muestraCrearContrasena', async () => {
    instalarServidorFalso({
      'GET /api/acceso/ingreso': {
        estado: 200,
        cuerpo: { contrasenaDefinida: false, bloqueadoHasta: null, sesionActiva: false },
      },
    });

    render(
      <MemoryRouter initialEntries={['/configuracion']}>
        <App />
      </MemoryRouter>,
    );

    expect(await screen.findByRole('heading', { name: 'Crear la contraseña' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Configuración' })).not.toBeInTheDocument();
  });

  it('App_sinConexion_muestraErrorYReintentarAvanza', async () => {
    const usuario = userEvent.setup();
    const servidor = instalarServidorFalso({
      'GET /api/acceso/ingreso': [
        { redCaida: true },
        { estado: 200, cuerpo: { contrasenaDefinida: true, bloqueadoHasta: null, sesionActiva: false } },
      ],
    });

    render(
      <MemoryRouter initialEntries={['/ingreso']}>
        <App />
      </MemoryRouter>,
    );

    expect((await screen.findByRole('alert')).textContent).toBe('No se pudo conectar con el sistema. Intentá de nuevo.');
    expect(screen.queryByRole('heading', { name: 'Ingresar' })).not.toBeInTheDocument();
    expect(servidor.pedidosA('GET', '/api/acceso/ingreso')).toHaveLength(1);

    await usuario.click(screen.getByRole('button', { name: 'Reintentar' }));

    expect(await screen.findByRole('heading', { name: 'Ingresar' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Reintentar' })).not.toBeInTheDocument();
    expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    expect(servidor.pedidosA('GET', '/api/acceso/ingreso')).toHaveLength(2);
  });

  it('App_estadoNoJson_muestraSinConexion', async () => {
    // Un 200 sin cuerpo JSON (por ejemplo, un proxy que responde otra cosa) no es un estado válido.
    instalarServidorFalso({ 'GET /api/acceso/ingreso': { estado: 200 } });

    render(
      <MemoryRouter initialEntries={['/ingreso']}>
        <App />
      </MemoryRouter>,
    );

    expect((await screen.findByRole('alert')).textContent).toBe('No se pudo conectar con el sistema. Intentá de nuevo.');
    expect(screen.getByRole('button', { name: 'Reintentar' })).toBeInTheDocument();
  });
});
