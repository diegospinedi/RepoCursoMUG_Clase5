import { StrictMode } from 'react';
import { createRoot } from 'react-dom/client';
import { BrowserRouter } from 'react-router-dom';
import { App } from './App';
import { aplicarMarca } from './marca/marca';
import './marca/marca.css';

// Los colores de la marca se publican antes del primer render (sin parpadeo).
aplicarMarca(document.documentElement);

const raiz = document.getElementById('raiz');
if (!raiz) {
  throw new Error('No se encontró el elemento #raiz en index.html.');
}

createRoot(raiz).render(
  <StrictMode>
    <BrowserRouter>
      <App />
    </BrowserRouter>
  </StrictMode>,
);
