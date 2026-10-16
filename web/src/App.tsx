import { useCallback, useEffect, useState, type ReactNode } from 'react';
import { Navigate, Route, Routes, useLocation, useNavigate } from 'react-router-dom';
import {
  EVENTO_SESION_VENCIDA,
  MENSAJE_SESION_VENCIDA,
  MENSAJE_SIN_CONEXION,
  obtenerEstadoAcceso,
  type EstadoAcceso,
} from './api/cliente';
import { Layout } from './componentes/Layout';
import { Configuracion } from './paginas/Configuracion';
import { CrearContrasena } from './paginas/CrearContrasena';
import { Ingreso } from './paginas/Ingreso';

type Pantalla = '/crear-contrasena' | '/ingreso' | '/configuracion';

/** La única pantalla válida para el estado del acceso (FR-01, FR-02). */
function pantallaPara(estado: EstadoAcceso): Pantalla {
  if (!estado.contrasenaDefinida) {
    return '/crear-contrasena';
  }

  return estado.sesionActiva ? '/configuracion' : '/ingreso';
}

/**
 * Guarda de ruteo: consulta `GET /api/acceso/ingreso` (que además emite el token antiforgery) y deja ver la
 * pantalla pedida solo si corresponde al estado; si no, redirige. La autorización real la hace el servidor:
 * esto solo decide qué pantalla mostrar.
 */
function Guarda({ pantalla, children }: { pantalla: Pantalla | null; children?: ReactNode }) {
  const navegar = useNavigate();
  const ubicacion = useLocation();
  const [habilitada, setHabilitada] = useState(false);
  const [falla, setFalla] = useState(false);
  const [intento, setIntento] = useState(0);

  useEffect(() => {
    let activa = true;
    setHabilitada(false);
    setFalla(false);
    obtenerEstadoAcceso()
      .then((estado) => {
        if (!activa) {
          return;
        }

        const destino = pantallaPara(estado);
        if (destino === pantalla) {
          setHabilitada(true);
        } else {
          navegar(destino, { replace: true, state: ubicacion.state as unknown });
        }
      })
      .catch(() => {
        if (activa) {
          setFalla(true);
        }
      });
    return () => {
      activa = false;
    };
    // El estado de navegación (el aviso) solo se reenvía: no dispara una nueva consulta.
  }, [pantalla, intento, navegar]);

  if (falla) {
    return (
      <Layout>
        <p className="mensaje mensaje--error" role="alert">
          {MENSAJE_SIN_CONEXION}
        </p>
        <button type="button" className="boton" onClick={() => setIntento((n) => n + 1)}>
          Reintentar
        </button>
      </Layout>
    );
  }

  if (!habilitada) {
    return (
      <Layout>
        <p role="status">Cargando…</p>
      </Layout>
    );
  }

  return <>{children}</>;
}

/** Rutas de la aplicación. El router (BrowserRouter en `main.tsx`, MemoryRouter en las pruebas) lo pone quien la usa. */
export function App() {
  const navegar = useNavigate();

  const sesionVencida = useCallback(() => {
    navegar('/ingreso', { replace: true, state: { aviso: MENSAJE_SESION_VENCIDA } });
  }, [navegar]);

  useEffect(() => {
    window.addEventListener(EVENTO_SESION_VENCIDA, sesionVencida);
    return () => window.removeEventListener(EVENTO_SESION_VENCIDA, sesionVencida);
  }, [sesionVencida]);

  return (
    <Routes>
      <Route path="/" element={<Guarda pantalla={null} />} />
      <Route
        path="/crear-contrasena"
        element={
          <Guarda pantalla="/crear-contrasena">
            <CrearContrasena />
          </Guarda>
        }
      />
      <Route
        path="/ingreso"
        element={
          <Guarda pantalla="/ingreso">
            <Ingreso />
          </Guarda>
        }
      />
      <Route
        path="/configuracion"
        element={
          <Guarda pantalla="/configuracion">
            <Configuracion />
          </Guarda>
        }
      />
      <Route path="*" element={<Navigate to="/" replace />} />
    </Routes>
  );
}
