import { describe, expect, it } from 'vitest';

// Todo el código fuente de la aplicación (sin tests ni ayudas de prueba), como texto.
const fuentes = import.meta.glob<string>(['/src/**/*.{ts,tsx,css}', '!/src/**/*.test.{ts,tsx}', '!/src/pruebas/**'], {
  query: '?raw',
  import: 'default',
  eager: true,
});

const COLOR_ESCRITO = /#[0-9a-f]{3,8}\b|rgba?\(/i;

describe('Marca', () => {
  it('Marca_sinColoresEscritosEnElCodigo', () => {
    const archivos = Object.keys(fuentes);
    // El recorrido tiene que ver el código de verdad, incluida la hoja de estilos.
    expect(archivos).toEqual(expect.arrayContaining(['/src/marca/marca.ts', '/src/marca/marca.css', '/src/App.tsx']));
    expect(archivos.some((archivo) => archivo.includes('.test.') || archivo.includes('/pruebas/'))).toBe(false);
    expect(fuentes['/src/marca/marca.css']).toContain('var(--color-primario)');

    const conColores = Object.entries(fuentes)
      .filter(([, texto]) => COLOR_ESCRITO.test(texto))
      .map(([archivo, texto]) => `${archivo}: ${texto.match(COLOR_ESCRITO)?.[0]}`);

    // FR-16: los colores solo pueden venir de Marca/branding.json.
    expect(conColores).toEqual([]);
  });
});
