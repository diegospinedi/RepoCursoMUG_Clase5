import {
  CONTRASENA_INICIAL,
  CONTRASENA_NUEVA,
  COOKIE_SESION,
  esperarConfiguracion,
  expect,
  ingresar,
  MENSAJE_SESION_VENCIDA,
  prepararBaseSinContrasena,
  test,
} from './soporte';

const CAMPOS_CONFIGURACION = ['alicuotaIva', 'condicionFiscal', 'topeIdentificacion', 'multiploRedondeo'];

// Flujo ordenado: cada test parte del estado que dejó el anterior. El archivo arranca siempre de una base sin
// contraseña ni intentos fallidos (beforeAll), así no depende de lo que haya corrido antes.
// Cada test usa un contexto de navegador nuevo (sin cookies): la sesión no pasa de un test a otro.
test.describe.serial('Acceso', () => {
  test.beforeAll(async ({}, testInfo) => {
    await prepararBaseSinContrasena(testInfo);
  });

  test('creación de contraseña e ingreso', async ({ page }) => {
    // Sin contraseña: cualquier ruta lleva a crear la contraseña (AC-02).
    await page.goto('/configuracion');
    await expect(page).toHaveURL(/\/crear-contrasena$/);
    await expect(page.getByRole('heading', { name: 'Crear la contraseña' })).toBeVisible();

    await page.getByLabel('Contraseña', { exact: true }).fill(CONTRASENA_INICIAL);
    await page.getByLabel('Repetí la contraseña', { exact: true }).fill(CONTRASENA_INICIAL);
    await page.getByRole('button', { name: 'Crear contraseña' }).click();

    // Crear la contraseña no inicia sesión: lleva a ingresar con ella.
    await expect(page).toHaveURL(/\/ingreso$/);
    await expect(page.getByText('La contraseña se creó. Ingresá con ella.')).toBeVisible();

    await page.getByLabel('Contraseña', { exact: true }).fill(CONTRASENA_INICIAL);
    await page.getByRole('button', { name: 'Ingresar' }).click();
    await esperarConfiguracion(page);

    // Ya definida, la pantalla de creación no se puede volver a usar.
    await page.goto('/crear-contrasena');
    await expect(page).toHaveURL(/\/configuracion$/);
  });

  test('sin sesión redirige a ingreso', async ({ page }) => {
    await page.goto('/configuracion');

    await expect(page).toHaveURL(/\/ingreso$/);
    await expect(page.getByRole('heading', { name: 'Ingresar' })).toBeVisible();
    await expect(page.getByRole('heading', { name: 'Configuración' })).toHaveCount(0);

    // La redirección es de la UI; la que protege los datos es la API (AC-01).
    const respuesta = await page.request.get('/api/configuracion');
    expect(respuesta.status()).toBe(401);
    // Sin datos de configuración en la respuesta (AC-01).
    const cuerpo = await respuesta.text();
    for (const campo of CAMPOS_CONFIGURACION) {
      expect(cuerpo).not.toContain(campo);
    }
  });

  test('cambio de contraseña', async ({ page, consola }) => {
    // El intento con la contraseña anterior responde 401 a propósito.
    consola.permitirRespuesta(401, '/api/acceso/ingreso');

    await ingresar(page, CONTRASENA_INICIAL);

    await page.getByLabel('Contraseña actual', { exact: true }).fill(CONTRASENA_INICIAL);
    await page.getByLabel('Contraseña nueva', { exact: true }).fill(CONTRASENA_NUEVA);
    await page.getByLabel('Repetí la contraseña nueva', { exact: true }).fill(CONTRASENA_NUEVA);
    await page.getByRole('button', { name: 'Cambiar contraseña' }).click();
    await expect(page.getByText('La contraseña se cambió')).toBeVisible();

    await page.getByRole('button', { name: 'Salir' }).click();
    await expect(page).toHaveURL(/\/ingreso$/);

    // La anterior ya no sirve (AC-05).
    await page.getByLabel('Contraseña', { exact: true }).fill(CONTRASENA_INICIAL);
    await page.getByRole('button', { name: 'Ingresar' }).click();
    await expect(page.getByRole('alert')).toHaveText('La contraseña no es correcta. Te quedan 4 intentos.');
    await expect(page).toHaveURL(/\/ingreso$/);

    // Solo la nueva.
    await page.getByLabel('Contraseña', { exact: true }).fill(CONTRASENA_NUEVA);
    await page.getByRole('button', { name: 'Ingresar' }).click();
    await esperarConfiguracion(page);
  });

  test('vencimiento redirige a ingreso', async ({ page, context, consola }) => {
    // Al perder la sesión, la API responde 401 (E6-1): es lo que se está probando.
    consola.permitirRespuesta(401, '/api/configuracion');

    await ingresar(page, CONTRASENA_NUEVA);
    expect((await context.cookies()).map((cookie) => cookie.name)).toContain(COOKIE_SESION);

    // Se simula el vencimiento borrando la cookie de sesión. El vencimiento real a los 60 minutos se prueba en la
    // API con FakeTimeProvider (Sesion_60MinutosInactiva_Devuelve401).
    await context.clearCookies({ name: COOKIE_SESION });
    expect((await context.cookies()).map((cookie) => cookie.name)).not.toContain(COOKIE_SESION);

    // La próxima acción contra la API vuelve 401 y la UI lleva a ingresar, con el aviso.
    await page.getByRole('button', { name: 'Grabar' }).click();

    await expect(page).toHaveURL(/\/ingreso$/);
    await expect(page.getByRole('heading', { name: 'Ingresar' })).toBeVisible();
    await expect(page.getByText(MENSAJE_SESION_VENCIDA)).toBeVisible();
  });
});
