import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { App } from '../App';
import { instalarServidorFalso } from '../pruebas/servidorFalso';

describe('cliente', () => {
  it('cliente_ante401_redirigeAIngreso', async () => {
    const servidor = instalarServidorFalso({
      'GET /api/acceso/ingreso': [
        { estado: 200, cuerpo: { contrasenaDefinida: true, bloqueadoHasta: null, sesionActiva: true } },
        { estado: 200, cuerpo: { contrasenaDefinida: true, bloqueadoHasta: null, sesionActiva: false } },
      ],
      // La sesión venció: el pedido autenticado vuelve 401.
      'GET /api/configuracion': { estado: 401 },
    });

    render(
      <MemoryRouter initialEntries={['/configuracion']}>
        <App />
      </MemoryRouter>,
    );

    expect(await screen.findByText('Tu sesión venció. Ingresá de nuevo.')).toBeInTheDocument();
    expect(screen.getByRole('heading', { name: 'Ingresar' })).toBeInTheDocument();
    expect(screen.queryByRole('heading', { name: 'Configuración' })).not.toBeInTheDocument();
    expect(servidor.pedidosA('GET', '/api/configuracion')).toHaveLength(1);
  });
});
