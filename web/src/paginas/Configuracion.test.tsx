import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';
import { EVENTO_SESION_VENCIDA } from '../api/cliente';
import { TOKEN_XSRF_DE_PRUEBA } from '../pruebas/constantes';
import { renderizarConRutas } from '../pruebas/rutas';
import { errorDeValidacion, instalarServidorFalso, type RespuestaFalsa } from '../pruebas/servidorFalso';
import { Configuracion } from './Configuracion';

const CONFIGURACION_INICIAL = {
  alicuotaIva: 21,
  condicionFiscal: 'ResponsableInscripto',
  topeIdentificacion: null,
  multiploRedondeo: 100,
};

const ETIQUETAS = {
  alicuota: 'Alícuota de IVA (%)',
  condicion: 'Condición fiscal',
  tope: 'Tope de identificación del receptor',
  multiplo: 'Múltiplo de redondeo',
};

const SIN_CONEXION = 'No se pudo conectar con el sistema. Intentá de nuevo.';

function renderizarConfiguracion(rutas: Record<string, RespuestaFalsa>) {
  const servidor = instalarServidorFalso({
    'GET /api/configuracion': { estado: 200, cuerpo: CONFIGURACION_INICIAL },
    ...rutas,
  });
  renderizarConRutas('/configuracion', [
    ['/configuracion', <Configuracion />],
    ['/ingreso', <p>Pantalla de ingreso</p>],
  ]);
  return servidor;
}

async function esperarCarga() {
  await waitFor(() => expect(screen.getByLabelText(ETIQUETAS.multiplo)).toHaveValue('100'));
}

async function reemplazar(usuario: UserEvent, etiqueta: string, texto: string) {
  const campo = screen.getByLabelText(etiqueta);
  await usuario.clear(campo);
  if (texto !== '') {
    await usuario.type(campo, texto);
  }
}

async function grabar(usuario: UserEvent) {
  await usuario.click(screen.getByRole('button', { name: 'Grabar' }));
}

async function cambiarContrasena(usuario: UserEvent, actual: string, nueva: string, repetida: string) {
  await usuario.type(screen.getByLabelText('Contraseña actual'), actual);
  await usuario.type(screen.getByLabelText('Contraseña nueva'), nueva);
  await usuario.type(screen.getByLabelText('Repetí la contraseña nueva'), repetida);
  await usuario.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));
}

/** El mensaje general de la sección "Cambiar contraseña" (no el de la configuración). */
function seccionContrasena() {
  return within(screen.getByRole('region', { name: 'Cambiar contraseña' }));
}

describe('Configuracion', () => {
  it('Configuracion_grabaYMuestraValores', async () => {
    const usuario = userEvent.setup();
    const guardada = {
      alicuotaIva: 10.5,
      condicionFiscal: 'Monotributo',
      topeIdentificacion: 250000.75,
      multiploRedondeo: 0.05,
    };
    const servidor = renderizarConfiguracion({ 'PUT /api/configuracion': { estado: 200, cuerpo: guardada } });

    // Muestra los valores cargados (coma decimal; tope vacío = sin cargar).
    await esperarCarga();
    expect(screen.getByLabelText(ETIQUETAS.alicuota)).toHaveValue('21');
    expect(screen.getByLabelText(ETIQUETAS.condicion)).toHaveValue('ResponsableInscripto');
    expect(screen.getByLabelText(ETIQUETAS.tope)).toHaveValue('');
    expect(screen.getByLabelText(ETIQUETAS.alicuota)).toHaveAttribute('inputmode', 'decimal');

    await reemplazar(usuario, ETIQUETAS.alicuota, '10,5');
    await usuario.selectOptions(screen.getByLabelText(ETIQUETAS.condicion), 'Monotributo');
    await reemplazar(usuario, ETIQUETAS.tope, '250.000,75');
    await reemplazar(usuario, ETIQUETAS.multiplo, '0,05');
    await grabar(usuario);

    expect(await screen.findByText('Configuración guardada')).toBeInTheDocument();
    const [pedido] = servidor.pedidosA('PUT', '/api/configuracion');
    expect(pedido?.cuerpo).toEqual(guardada);
    expect(pedido?.headers.get('X-XSRF-TOKEN')).toBe(TOKEN_XSRF_DE_PRUEBA);
    expect(pedido?.headers.get('Content-Type')).toBe('application/json');
    expect(pedido?.credentials).toBe('same-origin');
    expect(screen.getByLabelText(ETIQUETAS.alicuota)).toHaveValue('10,5');
    expect(screen.getByLabelText(ETIQUETAS.condicion)).toHaveValue('Monotributo');
    expect(screen.getByLabelText(ETIQUETAS.tope)).toHaveValue('250000,75');
    expect(screen.getByLabelText(ETIQUETAS.multiplo)).toHaveValue('0,05');
  });

  it.each([
    ['1.000', 1000],
    ['1.000,5', 1000.5],
    ['10,5', 10.5],
  ])('Configuracion_numeroEsAR_seEnviaComoNumber ("%s" → %d)', async (texto, numero) => {
    const usuario = userEvent.setup();
    const servidor = renderizarConfiguracion({
      'PUT /api/configuracion': { estado: 200, cuerpo: { ...CONFIGURACION_INICIAL, multiploRedondeo: numero } },
    });
    await esperarCarga();

    await reemplazar(usuario, ETIQUETAS.multiplo, texto);
    await grabar(usuario);

    expect(await screen.findByText('Configuración guardada')).toBeInTheDocument();
    expect(servidor.pedidosA('PUT', '/api/configuracion')[0]?.cuerpo).toMatchObject({ multiploRedondeo: numero });
  });

  it.each(['0.050', '0.500'])(
    'Configuracion_decimalConPunto_pideComaYNoEnvia ("%s")',
    async (texto) => {
      const usuario = userEvent.setup();
      const servidor = renderizarConfiguracion({
        'PUT /api/configuracion': { estado: 200, cuerpo: CONFIGURACION_INICIAL },
      });
      await esperarCarga();

      await reemplazar(usuario, ETIQUETAS.multiplo, texto);
      await grabar(usuario);

      expect(await screen.findByText('Usá coma para los decimales')).toBeInTheDocument();
      expect(screen.getByLabelText(ETIQUETAS.multiplo)).toHaveAccessibleDescription('Usá coma para los decimales');
      expect(servidor.pedidosA('PUT', '/api/configuracion')).toHaveLength(0);
    },
  );

  it('Configuracion_multiploInvalido_muestraMensaje', async () => {
    const usuario = userEvent.setup();
    const servidor = renderizarConfiguracion({
      'PUT /api/configuracion': errorDeValidacion({ multiploRedondeo: ['El valor mínimo es 0,01'] }),
    });
    await esperarCarga();

    await reemplazar(usuario, ETIQUETAS.multiplo, '0,001');
    await grabar(usuario);

    expect(await screen.findByText('El valor mínimo es 0,01')).toBeInTheDocument();
    expect(screen.getByLabelText(ETIQUETAS.multiplo)).toHaveAccessibleDescription('El valor mínimo es 0,01');
    expect(screen.getByLabelText(ETIQUETAS.multiplo)).toHaveAttribute('aria-invalid', 'true');
    expect(servidor.pedidosA('PUT', '/api/configuracion')[0]?.cuerpo).toMatchObject({ multiploRedondeo: 0.001 });
    expect(screen.queryByText('Configuración guardada')).not.toBeInTheDocument();
  });

  it('Configuracion_alicuotaInvalida_muestraMensaje', async () => {
    const usuario = userEvent.setup();
    renderizarConfiguracion({
      'PUT /api/configuracion': errorDeValidacion({ alicuotaIva: ['La alícuota de IVA debe estar entre 0 y 100'] }),
    });
    await esperarCarga();

    await reemplazar(usuario, ETIQUETAS.alicuota, '101');
    await grabar(usuario);

    expect(await screen.findByText('La alícuota de IVA debe estar entre 0 y 100')).toBeInTheDocument();
    expect(screen.getByLabelText(ETIQUETAS.alicuota)).toHaveAccessibleDescription(
      'La alícuota de IVA debe estar entre 0 y 100',
    );
  });

  it('Configuracion_topeNegativo_muestraMensaje', async () => {
    const usuario = userEvent.setup();
    const servidor = renderizarConfiguracion({
      'PUT /api/configuracion': errorDeValidacion({ topeIdentificacion: ['El tope no puede ser negativo'] }),
    });
    await esperarCarga();

    await reemplazar(usuario, ETIQUETAS.tope, '-5');
    await grabar(usuario);

    expect(await screen.findByText('El tope no puede ser negativo')).toBeInTheDocument();
    expect(screen.getByLabelText(ETIQUETAS.tope)).toHaveAccessibleDescription('El tope no puede ser negativo');
    expect(servidor.pedidosA('PUT', '/api/configuracion')[0]?.cuerpo).toMatchObject({ topeIdentificacion: -5 });
  });

  it('Configuracion_valorNoNumerico_noEnvia', async () => {
    const usuario = userEvent.setup();
    const servidor = renderizarConfiguracion({
      'PUT /api/configuracion': { estado: 200, cuerpo: CONFIGURACION_INICIAL },
    });
    await esperarCarga();

    await reemplazar(usuario, ETIQUETAS.alicuota, 'veintiuno');
    await grabar(usuario);

    expect(await screen.findByText('Ingresá un número')).toBeInTheDocument();
    expect(screen.getByLabelText(ETIQUETAS.alicuota)).toHaveAccessibleDescription('Ingresá un número');
    expect(servidor.pedidosA('PUT', '/api/configuracion')).toHaveLength(0);
  });

  it('Configuracion_actualIncorrecta_muestraMensaje', async () => {
    const usuario = userEvent.setup();
    const servidor = renderizarConfiguracion({
      'PUT /api/acceso/contrasena': errorDeValidacion({ contrasenaActual: ['La contraseña actual no es correcta'] }),
    });
    await esperarCarga();

    await usuario.type(screen.getByLabelText('Contraseña actual'), 'no-es-la-actual');
    await usuario.type(screen.getByLabelText('Contraseña nueva'), 'una-nueva-larga');
    await usuario.type(screen.getByLabelText('Repetí la contraseña nueva'), 'una-nueva-larga');
    await usuario.click(screen.getByRole('button', { name: 'Cambiar contraseña' }));

    expect(await screen.findByText('La contraseña actual no es correcta')).toBeInTheDocument();
    expect(screen.getByLabelText('Contraseña actual')).toHaveAccessibleDescription('La contraseña actual no es correcta');
    expect(servidor.pedidosA('PUT', '/api/acceso/contrasena')[0]?.cuerpo).toEqual({
      contrasenaActual: 'no-es-la-actual',
      contrasenaNueva: 'una-nueva-larga',
    });
    // La sesión sigue: un 400 no es un vencimiento.
    expect(screen.queryByText('Pantalla de ingreso')).not.toBeInTheDocument();
  });

  it('Configuracion_errorDeRed_conservaLoCargado', async () => {
    const usuario = userEvent.setup();
    renderizarConfiguracion({ 'PUT /api/configuracion': { redCaida: true } });
    await esperarCarga();

    await reemplazar(usuario, ETIQUETAS.alicuota, '10,5');
    await reemplazar(usuario, ETIQUETAS.tope, '5000');
    await reemplazar(usuario, ETIQUETAS.multiplo, '50');
    await grabar(usuario);

    expect(await screen.findByText(SIN_CONEXION)).toBeInTheDocument();
    expect(screen.getByLabelText(ETIQUETAS.alicuota)).toHaveValue('10,5');
    expect(screen.getByLabelText(ETIQUETAS.tope)).toHaveValue('5000');
    expect(screen.getByLabelText(ETIQUETAS.multiplo)).toHaveValue('50');
    expect(screen.queryByText('Configuración guardada')).not.toBeInTheDocument();
  });

  it.each([
    [500, 'Ocurrió un error inesperado'],
    [503, 'La base está ocupada, intentá de nuevo'],
  ])('Configuracion_error%d_conservaLoCargado', async (estado, titulo) => {
    const usuario = userEvent.setup();
    renderizarConfiguracion({ 'PUT /api/configuracion': { estado, cuerpo: { title: titulo, status: estado } } });
    await esperarCarga();

    await reemplazar(usuario, ETIQUETAS.alicuota, '10,5');
    await reemplazar(usuario, ETIQUETAS.tope, '5000');
    await reemplazar(usuario, ETIQUETAS.multiplo, '50');
    await grabar(usuario);

    expect(await screen.findByText(SIN_CONEXION)).toBeInTheDocument();
    expect(screen.getByLabelText(ETIQUETAS.alicuota)).toHaveValue('10,5');
    expect(screen.getByLabelText(ETIQUETAS.tope)).toHaveValue('5000');
    expect(screen.getByLabelText(ETIQUETAS.multiplo)).toHaveValue('50');
    expect(screen.queryByText('Configuración guardada')).not.toBeInTheDocument();
    expect(screen.queryByText('Pantalla de ingreso')).not.toBeInTheDocument();
  });

  it.each([
    ['red caída', { redCaida: true } as RespuestaFalsa, SIN_CONEXION],
    ['403', { estado: 403, cuerpo: { title: 'Prohibido', status: 403 } } as RespuestaFalsa, 'Prohibido'],
  ])('Configuracion_cargaFalla_%s_muestraMensajeSinFormulario', async (_caso, respuesta, esperado) => {
    renderizarConfiguracion({ 'GET /api/configuracion': respuesta });

    expect((await screen.findByRole('alert')).textContent).toBe(esperado);
    expect(screen.queryByLabelText(ETIQUETAS.alicuota)).not.toBeInTheDocument();
    expect(screen.queryByText('Cargando…')).not.toBeInTheDocument();
  });

  it('Configuracion_cambioContrasena_confirmacionDistinta_noEnvia', async () => {
    const usuario = userEvent.setup();
    const servidor = renderizarConfiguracion({ 'PUT /api/acceso/contrasena': { estado: 204 } });
    await esperarCarga();

    await cambiarContrasena(usuario, 'la-actual-123', 'una-nueva-larga', 'otra-nueva-larga');

    expect(await screen.findByText('Las contraseñas no coinciden')).toBeInTheDocument();
    expect(screen.getByLabelText('Repetí la contraseña nueva')).toHaveAccessibleDescription('Las contraseñas no coinciden');
    expect(servidor.pedidosA('PUT', '/api/acceso/contrasena')).toHaveLength(0);
    expect(screen.queryByText('La contraseña se cambió')).not.toBeInTheDocument();
  });

  it.each([
    ['400 sin campos de contraseña', errorDeValidacion({ otroCampo: ['No es válido'] }), 'Revisá los datos ingresados'],
    ['429', { estado: 429, cuerpo: { title: 'Demasiados intentos', status: 429 } } as RespuestaFalsa, 'Demasiados intentos'],
    ['red caída', { redCaida: true } as RespuestaFalsa, SIN_CONEXION],
    ['500', { estado: 500, cuerpo: { title: 'Ocurrió un error inesperado', status: 500 } } as RespuestaFalsa, SIN_CONEXION],
  ])('Configuracion_cambioContrasena_%s_muestraMensajeGeneral', async (_caso, respuesta, esperado) => {
    const usuario = userEvent.setup();
    renderizarConfiguracion({ 'PUT /api/acceso/contrasena': respuesta });
    await esperarCarga();

    await cambiarContrasena(usuario, 'la-actual-123', 'una-nueva-larga', 'una-nueva-larga');

    expect((await seccionContrasena().findByRole('alert')).textContent).toBe(esperado);
    expect(screen.getByLabelText('Contraseña actual')).not.toHaveAttribute('aria-invalid');
    expect(screen.getByLabelText('Contraseña nueva')).not.toHaveAttribute('aria-invalid');
    expect(screen.queryByText('La contraseña se cambió')).not.toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Cambiar contraseña' })).toBeEnabled();
  });

  it('Configuracion_cambioContrasena_nuevaInvalida_muestraMensajeJuntoAlCampo', async () => {
    const usuario = userEvent.setup();
    renderizarConfiguracion({
      'PUT /api/acceso/contrasena': errorDeValidacion({ contrasenaNueva: ['La contraseña debe tener al menos 8 caracteres'] }),
    });
    await esperarCarga();

    await cambiarContrasena(usuario, 'la-actual-123', 'corta', 'corta');

    const campo = screen.getByLabelText('Contraseña nueva');
    await waitFor(() => expect(campo).toHaveAccessibleDescription('La contraseña debe tener al menos 8 caracteres'));
    expect(screen.getByLabelText('Contraseña actual')).not.toHaveAttribute('aria-invalid');
    expect(seccionContrasena().queryByRole('alert')).not.toBeInTheDocument();
  });

  it('Configuracion_cambioContrasena_sesionVencida_avisaSinMensaje', async () => {
    const usuario = userEvent.setup();
    const aviso = vi.fn();
    window.addEventListener(EVENTO_SESION_VENCIDA, aviso);
    try {
      const servidor = renderizarConfiguracion({ 'PUT /api/acceso/contrasena': { estado: 401 } });
      await esperarCarga();

      await cambiarContrasena(usuario, 'la-actual-123', 'una-nueva-larga', 'una-nueva-larga');

      await waitFor(() => expect(aviso).toHaveBeenCalledTimes(1));
      await waitFor(() => expect(screen.getByRole('button', { name: 'Cambiar contraseña' })).toBeEnabled());
      expect(servidor.pedidosA('PUT', '/api/acceso/contrasena')).toHaveLength(1);
      expect(screen.queryByRole('alert')).not.toBeInTheDocument();
      expect(screen.queryByText('La contraseña se cambió')).not.toBeInTheDocument();
    } finally {
      window.removeEventListener(EVENTO_SESION_VENCIDA, aviso);
    }
  });
});
