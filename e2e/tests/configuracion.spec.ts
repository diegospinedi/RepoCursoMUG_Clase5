import { type Page } from '@playwright/test';
import {
  COLOR_FONDO,
  COLOR_PRIMARIO,
  CONTRASENA_CONFIGURACION,
  expect,
  ingresar,
  prepararBaseConContrasena,
  test,
} from './soporte';

// El archivo prepara su propia contraseña por API (blanqueo + creación inicial): no depende de acceso.spec.ts ni
// de su resultado, y nunca ingresa con una contraseña que no sea la que acaba de crear.
test.describe.serial('Configuración', () => {
  test.beforeAll(async ({}, testInfo) => {
    await prepararBaseConContrasena(testInfo, CONTRASENA_CONFIGURACION);
  });

  test('marca visible en ingreso y configuración', async ({ page }) => {
    await page.goto('/ingreso');
    await expect(page.getByRole('heading', { name: 'Ingresar' })).toBeVisible();
    await verificarMarca(page);

    await ingresar(page, CONTRASENA_CONFIGURACION);
    await verificarMarca(page);
  });

  test('configuración: graba y muestra los 4 valores; múltiplo 0,001 muestra error', async ({ page, consola }) => {
    // El múltiplo 0,001 lo rechaza la API con 400 a propósito (AC-13).
    consola.permitirRespuesta(400, '/api/configuracion');

    await ingresar(page, CONTRASENA_CONFIGURACION);
    const alicuota = page.getByLabel('Alícuota de IVA (%)', { exact: true });
    const condicion = page.getByLabel('Condición fiscal', { exact: true });
    const tope = page.getByLabel('Tope de identificación del receptor', { exact: true });
    const multiplo = page.getByLabel('Múltiplo de redondeo', { exact: true });

    // Los 4 valores de AC-12.
    await alicuota.fill('10,5');
    await condicion.selectOption('Monotributo');
    await tope.fill('100000');
    await multiplo.fill('50');
    await page.getByRole('button', { name: 'Grabar' }).click();
    await expect(page.getByText('Configuración guardada')).toBeVisible();

    // Al volver a abrir la pantalla se leen de la API: quedaron en la base.
    await page.reload();
    await expect(multiplo).toHaveValue('50');
    await expect(alicuota).toHaveValue('10,5');
    await expect(condicion).toHaveValue('Monotributo');
    await expect(tope).toHaveValue('100000');

    // Múltiplo 0,001: la API lo rechaza y el mensaje queda junto al campo (AC-13, FR-17).
    await multiplo.fill('0,001');
    await page.getByRole('button', { name: 'Grabar' }).click();
    await expect(page.getByText('El valor mínimo es 0,01')).toBeVisible();
    // El navegador incluye en la descripción el prefijo decorativo "⚠ " que agrega marca.css (::before).
    await expect(multiplo).toHaveAccessibleDescription(/^(⚠\s*)?El valor mínimo es 0,01$/);
    await expect(multiplo).toHaveAttribute('aria-invalid', 'true');
    await expect(page.getByText('Configuración guardada')).toHaveCount(0);

    // No se grabó nada: siguen los valores anteriores.
    await page.reload();
    await expect(multiplo).toHaveValue('50');
    await expect(alicuota).toHaveValue('10,5');
    await expect(condicion).toHaveValue('Monotributo');
    await expect(tope).toHaveValue('100000');
  });
});

/** Logo de Marca/ cargado y colores de Marca/branding.json aplicados (AC-17). */
async function verificarMarca(page: Page): Promise<void> {
  const logo = page.getByRole('banner').getByRole('img', { name: 'Óptica Sistema' });
  await expect(logo).toBeVisible();
  // La imagen se descargó y decodificó (no la bloqueó el CSP ni falló la descarga).
  await expect
    .poll(() => logo.evaluate((imagen: HTMLImageElement) => imagen.complete && imagen.naturalWidth > 0))
    .toBe(true);

  const encabezado = page.getByRole('banner');
  await expect(encabezado).toHaveCSS('border-bottom-color', COLOR_PRIMARIO);
  await expect(encabezado).toHaveCSS('background-color', COLOR_FONDO);
  await expect(page.locator('body')).toHaveCSS('color', COLOR_PRIMARIO);
}
