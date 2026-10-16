import { fileURLToPath } from 'node:url';
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vitest/config';

const directorioMarca = fileURLToPath(new URL('../Marca', import.meta.url));

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      // La marca (branding.json y logo.png) vive en la raíz del repo y es la única fuente de colores y logo (FR-16).
      '@marca': directorioMarca,
    },
  },
  server: {
    host: '127.0.0.1',
    fs: {
      // Permite servir ../Marca en desarrollo (spec). Como eso abre la raíz del repo, se niega todo archivo
      // sensible que pueda haber ahí: certificados de ARCA, claves de Data Protection, secretos, bases y logs.
      allow: ['..'],
      // Al definir deny se reemplazan los valores por defecto de Vite: se repiten acá.
      deny: [
        // Valores por defecto de Vite 8.
        '.env',
        '.env.*',
        '*.{crt,pem,key,p12,pfx,cer,der}',
        '.npmrc',
        '.yarnrc.yml',
        '**/.git/**',
        // Propios del proyecto.
        '**/*.pfx',
        '**/*.p12',
        '**/key-*.xml',
        '**/secrets.json',
        '**/appsettings.*.local.json',
        '**/*.db*',
        '**/*.log',
      ],
    },
    proxy: {
      '/api': { target: 'http://127.0.0.1:5080', changeOrigin: false },
    },
  },
  build: {
    outDir: 'dist',
    emptyOutDir: true,
    // Sin source maps en el publish: no se exponen las fuentes.
    sourcemap: false,
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./vitest.setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    restoreMocks: true,
    // Procesa las hojas de estilo propias para que las pruebas puedan leerlas y verificar que no tienen colores
    // escritos a mano (FR-16).
    css: { include: [/\/src\/.*\.css/] },
    // `npm run test:coverage`: el mínimo es 80 % en las cuatro métricas (F-VER-03). El directorio de salida
    // (`coverage/`) no se commitea.
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: ['**/*.test.*', 'src/pruebas/**', 'src/main.tsx'],
      reporter: ['text', 'json-summary'],
      thresholds: { lines: 80, branches: 80, functions: 80, statements: 80 },
    },
  },
});
