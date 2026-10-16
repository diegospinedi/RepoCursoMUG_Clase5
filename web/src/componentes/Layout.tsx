import { useLayoutEffect, useState, type ReactNode } from 'react';
import { useNavigate } from 'react-router-dom';
import { ErrorConexion, ErrorSesionVencida, salir } from '../api/cliente';
import { aplicarMarca, marca } from '../marca/marca';
import '../marca/marca.css';

interface PropsLayout {
  children: ReactNode;
  /** Muestra el botón Salir (solo en las pantallas con sesión). */
  conSesion?: boolean;
}

/** Marco común de todas las pantallas: logo y colores de la marca (FR-16) y, con sesión, el botón Salir. */
export function Layout({ children, conSesion = false }: PropsLayout) {
  const navegar = useNavigate();
  const [error, setError] = useState<string | null>(null);
  const [saliendo, setSaliendo] = useState(false);

  useLayoutEffect(() => {
    aplicarMarca(document.documentElement);
  }, []);

  async function cerrarSesion() {
    setError(null);
    setSaliendo(true);
    try {
      await salir();
      navegar('/ingreso', { replace: true });
    } catch (causa) {
      // Con la sesión vencida la aplicación ya vuelve a ingreso.
      if (!(causa instanceof ErrorSesionVencida)) {
        setError(causa instanceof ErrorConexion ? causa.message : 'No se pudo cerrar la sesión. Intentá de nuevo.');
      }
    } finally {
      setSaliendo(false);
    }
  }

  return (
    <>
      <header className="encabezado">
        <img className="encabezado__logo" src={marca.logo} alt={marca.nombre} />
        {conSesion ? (
          <button type="button" className="boton boton--secundario" onClick={cerrarSesion} disabled={saliendo}>
            Salir
          </button>
        ) : null}
      </header>
      <main className="contenido">
        {error ? (
          <p className="mensaje mensaje--error" role="alert">
            {error}
          </p>
        ) : null}
        {children}
      </main>
    </>
  );
}
