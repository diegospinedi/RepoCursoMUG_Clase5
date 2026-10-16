import { render, screen } from '@testing-library/react';
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
});
