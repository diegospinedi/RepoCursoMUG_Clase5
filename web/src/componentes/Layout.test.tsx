import { colores } from '@marca/branding.json';
import logo from '@marca/logo.png';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import cssMarca from '../marca/marca.css?raw';
import { TOKEN_XSRF_DE_PRUEBA } from '../pruebas/constantes';
import { renderizarConRutas } from '../pruebas/rutas';
import { instalarServidorFalso } from '../pruebas/servidorFalso';
import { Layout } from './Layout';

describe('Layout', () => {
  it('Layout_muestraLogoYColores', () => {
    const { container } = render(
      <MemoryRouter>
        <Layout>
          <p>contenido</p>
        </Layout>
      </MemoryRouter>,
    );

    // Logo de Marca/logo.png en el encabezado.
    const imagen = screen.getByRole('img', { name: 'Óptica Sistema' });
    expect(imagen).toHaveAttribute('src', logo);
    expect(screen.getByRole('banner')).toContainElement(imagen);
    expect(screen.getByText('contenido')).toBeInTheDocument();

    // Los colores salen de branding.json (no escritos a mano) y se publican como variables CSS.
    const raiz = document.documentElement.style;
    expect(raiz.getPropertyValue('--color-primario')).toBe(colores.primario);
    expect(raiz.getPropertyValue('--color-fondo')).toBe(colores.fondo);

    // La hoja de estilos usa las variables.
    expect(cssMarca).toContain('var(--color-primario)');
    expect(cssMarca).toContain('var(--color-fondo)');

    // CSP sin 'unsafe-inline': ningún elemento renderizado lleva atributo style.
    expect(container.querySelectorAll('[style]')).toHaveLength(0);
  });

  it('Layout_salir_cierraSesionYVaAIngreso', async () => {
    const usuario = userEvent.setup();
    const servidor = instalarServidorFalso({ 'POST /api/acceso/salida': { estado: 204 } });
    renderizarConRutas('/configuracion', [
      [
        '/configuracion',
        <Layout conSesion>
          <p>Pantalla de configuración</p>
        </Layout>,
      ],
      ['/ingreso', <p>Pantalla de ingreso</p>],
    ]);

    await usuario.click(screen.getByRole('button', { name: 'Salir' }));

    expect(await screen.findByText('Pantalla de ingreso')).toBeInTheDocument();
    const [pedido] = servidor.pedidosA('POST', '/api/acceso/salida');
    expect(pedido?.headers.get('X-XSRF-TOKEN')).toBe(TOKEN_XSRF_DE_PRUEBA);
    expect(pedido?.credentials).toBe('same-origin');
  });
});
