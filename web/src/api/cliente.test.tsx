import { render, screen } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { describe, expect, it } from 'vitest';
import { ErrorApi, ErrorConexion, ingresar, obtenerEstadoAcceso } from './cliente';
import { App } from '../App';
import { instalarServidorFalso } from '../pruebas/servidorFalso';

const ESTADO = { contrasenaDefinida: true, bloqueadoHasta: null, sesionActiva: false };

function borrarCookieXsrf() {
  document.cookie = 'XSRF-TOKEN=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/';
}

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

  it('cliente_sinCookieXsrf_pideElEstadoYDespuesMandaElToken', async () => {
    borrarCookieXsrf();
    // Una cookie sin "=" y otra con un nombre parecido no se confunden con la de XSRF.
    document.cookie = 'sinvalor; path=/';
    document.cookie = 'XSRF-TOKEN-ANTERIOR=viejo; path=/';
    const servidor = instalarServidorFalso({
      'GET /api/acceso/ingreso': () => {
        // El servidor reemite la cookie al consultar el estado.
        document.cookie = 'XSRF-TOKEN=token%20reemitido; path=/';
        return { estado: 200, cuerpo: ESTADO };
      },
      'POST /api/acceso/ingreso': { estado: 204 },
    });

    await ingresar('una-contrasena');

    expect(servidor.pedidos.map((p) => `${p.metodo} ${p.ruta}`)).toEqual([
      'GET /api/acceso/ingreso',
      'POST /api/acceso/ingreso',
    ]);
    expect(servidor.pedidos[0]?.headers.has('X-XSRF-TOKEN')).toBe(false);
    expect(servidor.pedidos[1]?.headers.get('X-XSRF-TOKEN')).toBe('token reemitido');
  });

  it('cliente_cookieXsrfMalCodificada_noMandaToken', async () => {
    borrarCookieXsrf();
    document.cookie = 'XSRF-TOKEN=%E0%A4%A; path=/';
    const servidor = instalarServidorFalso({
      'GET /api/acceso/ingreso': { estado: 200, cuerpo: ESTADO },
      'POST /api/acceso/ingreso': { estado: 204 },
    });

    await ingresar('una-contrasena');

    // La cookie ilegible cuenta como ausente: pide el estado para que se reemita y, si sigue mal, no inventa un token.
    expect(servidor.pedidosA('GET', '/api/acceso/ingreso')).toHaveLength(1);
    const [pedido] = servidor.pedidosA('POST', '/api/acceso/ingreso');
    expect(pedido?.headers.has('X-XSRF-TOKEN')).toBe(false);
  });

  it('cliente_respuestaOkSinJson_esErrorDeConexion', async () => {
    instalarServidorFalso({ 'GET /api/acceso/ingreso': { estado: 200 } });

    await expect(obtenerEstadoAcceso()).rejects.toBeInstanceOf(ErrorConexion);
  });

  it('cliente_errorSinJson_usaTituloPorDefecto', async () => {
    instalarServidorFalso({ 'POST /api/acceso/ingreso': { estado: 400 } });

    const error = await ingresar('una-contrasena').catch((causa: unknown) => causa);

    expect(error).toBeInstanceOf(ErrorApi);
    expect(error).toMatchObject({ estado: 400, titulo: 'El pedido no es válido', campos: {}, extensiones: {} });
  });

  it('cliente_errores_soloTomaMensajesDeTexto', async () => {
    instalarServidorFalso({
      'POST /api/acceso/ingreso': {
        estado: 400,
        cuerpo: {
          title: 'Revisá los datos ingresados',
          status: 400,
          errors: { contrasena: ['Muy corta', 3, 'Sin números'], vacio: [7], noArreglo: 'texto suelto' },
          traceId: 'abc',
          campoExtra: 'se conserva',
        },
      },
    });

    const error = await ingresar('una-contrasena').catch((causa: unknown) => causa);

    expect(error).toBeInstanceOf(ErrorApi);
    expect((error as ErrorApi).campos).toEqual({ contrasena: 'Muy corta Sin números' });
    expect((error as ErrorApi).extensiones).toEqual({ campoExtra: 'se conserva' });
  });
});
