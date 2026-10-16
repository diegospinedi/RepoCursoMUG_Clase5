import { render } from '@testing-library/react';
import type { ReactElement } from 'react';
import { MemoryRouter, Route, Routes } from 'react-router-dom';

/** Renderiza un conjunto de rutas en memoria, empezando en `inicio`. */
export function renderizarConRutas(inicio: string, rutas: Array<[string, ReactElement]>) {
  return render(
    <MemoryRouter initialEntries={[inicio]}>
      <Routes>
        {rutas.map(([ruta, elemento]) => (
          <Route key={ruta} path={ruta} element={elemento} />
        ))}
      </Routes>
    </MemoryRouter>,
  );
}
