import { colores } from '@marca/branding.json';
import logo from '@marca/logo.png';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it, vi } from 'vitest';
import { EVENTO_SESION_VENCIDA } from '../api/cliente';
import cssMarca from '../marca/marca.css?raw';
import { TOKEN_XSRF_DE_PRUEBA } from '../pruebas/constantes';
import { renderizarConRutas } from '../pruebas/rutas';
import { instalarServidorFalso, type RespuestaFalsa } from '../pruebas/servidorFalso';
import { Layout } from './Layout';

function renderizarSalida() {
  return renderizarConRutas('/configuracion', [
    [
      '/configuracion',
      <Layout conSesion>
        <p>Pantalla de configuración</p>
      </Layout>,
    ],
    ['/ingreso', <p>Pantalla de ingreso</p>],
  ]);
}

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

  it.each([
    ['red caída', { redCaida: true } as RespuestaFalsa, 'No se pudo conectar con el sistema. Intentá de nuevo.'],
    ['500', { estado: 500 } as RespuestaFalsa, 'No se pudo conectar con el sistema. Intentá de nuevo.'],
    ['403', { estado: 403, cuerpo: { title: 'Prohibido', status: 403 } } as RespuestaFalsa, 'No se pudo cerrar la sesión. Intentá de nuevo.'],
  ])('Layout_salir_%s_muestraErrorYSeQueda', async (_caso, respuesta, esperado) => {
    const usuario = userEvent.setup();
    instalarServidorFalso({ 'POST /api/acceso/salida': respuesta });
    renderizarSalida();

    await usuario.click(screen.getByRole('button', { name: 'Salir' }));

    expect((await screen.findByRole('alert')).textContent).toBe(esperado);
    expect(screen.getByText('Pantalla de configuración')).toBeInTheDocument();
    expect(screen.queryByText('Pantalla de ingreso')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Salir' })).toBeEnabled();
  });

  it('Layout_salir_sesionVencida_noMuestraErrorYAvisa', async () => {
    const usuario = userEvent.setup();
    const aviso = vi.fn();
    window.addEventListener(EVENTO_SESION_VENCIDA, aviso);
    try {
      instalarServidorFalso({ 'POST /api/acceso/salida': { estado: 401 } });
      renderizarSalida();

      await usuario.click(screen.getByRole('button', { name: 'Salir' }));

      await waitFor(() => expect(aviso).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(screen.getByRole('button', { name: 'Salir' })).toBeEnabled());
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
    } finally {
      window.removeEventListener(EVENTO_SESION_VENCIDA, aviso);
    }
  });
});
